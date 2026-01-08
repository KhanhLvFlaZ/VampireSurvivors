# Cooperative Decision Validation Guide

## Overview

This guide explains how to validate and coordinate multi-agent decisions for emergent team behaviors in Co-op Survivors RL. Ensures agents act cohesively on shared objectives (focus target, formation, aggro distribution).

## Problem: Uncoordinated Decisions

Without coordination validation, multi-agent RL can result in:

- All agents attacking different targets (wasted focus fire)
- Monsters bunched together (no formation, easy AOE kill)
- All tanks ignoring aggro (no threat management)
- Chaotic healing/support patterns

## Solution: Cooperative Decision Validation

Three-layer validation ensures coordinated team behaviors:

```
Layer 1: Pre-Decision
├─ Team Composition Analysis
│  ├─ Detect tank/support availability
│  ├─ Assess team positioning
│  └─ Update team focus target
├─ Context Enrichment
│  ├─ Broadcast focus target to all agents
│  └─ Communicate threat levels
└─ Initialize Coordination State

Layer 2: Post-Inference Validation
├─ Focus Fire Validation
│  ├─ Check if agents target team focus target
│  ├─ Penalize divergence
│  └─ Broadcast action to support coordination
├─ Formation Validation
│  ├─ Check spread between teammates
│  ├─ Suggest repositioning
│  └─ Track formation score
└─ Aggro Share Validation
   ├─ Detect low-health agents
   ├─ Encourage tank to hold aggro
   └─ Adjust action priority

Layer 3: Post-Execution Monitoring
├─ Track Coordination Metrics
│  ├─ Team focus accuracy (% on target)
│  ├─ Formation maintenance (avg spread)
│  ├─ Aggro distribution fairness
│  └─ Coordination bonus earned
├─ Adjust for Next Decision
│  ├─ Decay focus target if stale
│  ├─ Update threat estimation
│  └─ Learn from coordination failure
└─ Reward Signals
   ├─ Issue cooperative bonuses
   ├─ Penalize uncoordination
   └─ Log metrics for training
```

## Implementation

### Step 1: Create Cooperation Validator

```csharp
// File: Assets/Scripts/RL/Core/CooperativeDecisionValidator.cs

using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Validates and adjusts multi-agent decisions for team coordination
    /// Ensures focus fire, formation, and aggro distribution
    /// </summary>
    public class CooperativeDecisionValidator
    {
        [System.Serializable]
        public class CoordinationMetrics
        {
            public float focusFireAccuracy;     // % of agents on focus target
            public float formationScore;        // Avg distance variance
            public float aggroDistribution;     // How evenly damage is spread
            public float coordinationBonus;     // Team reward multiplier
        }

        private readonly Dictionary<int, CoordinationMetrics> agentMetrics = new Dictionary<int, CoordinationMetrics>();

        // Team state
        private Vector2 teamFocusTarget = Vector2.zero;
        private int tankAgentId = -1;           // Tank (primary aggro holder)
        private List<int> supportAgentIds = new List<int>();
        private List<int> dpsAgentIds = new List<int>();

        // Configuration
        [SerializeField] private float focusFireThreshold = 0.7f;       // Need 70% on focus target
        [SerializeField] private float optimalFormationSpread = 5f;     // Optimal distance between agents
        [SerializeField] private float formationToleranceSq = 2f;       // Tolerance for formation
        [SerializeField] private float focusFireBonus = 1.5f;           // Multiplier when coordinated
        [SerializeField] private float formationBonus = 1.2f;           // Multiplier for formation
        [SerializeField] private float aggroShareBonus = 1.3f;          // Multiplier for aggro share
        [SerializeField] private float focusTargetDecayRate = 0.95f;   // Decay focus target over time
        [SerializeField] private int focusTargetUpdateIntervalFrames = 30;

        private int framesSinceFocusUpdate = 0;

        /// <summary>
        /// Initialize validator with team composition
        /// </summary>
        public void Initialize(int tankId, List<int> supportIds, List<int> dpsIds)
        {
            tankAgentId = tankId;
            supportAgentIds = new List<int>(supportIds);
            dpsAgentIds = new List<int>(dpsIds);

            Debug.Log($"[CoopValidator] Initialized: Tank={tankId}, Support={supportIds.Count}, DPS={dpsIds.Count}");
        }

        /// <summary>
        /// Update team focus target based on current threats
        /// Called once per decision cycle
        /// </summary>
        public void UpdateTeamFocusTarget(Vector2 highestThreatPosition)
        {
            if (framesSinceFocusUpdate >= focusTargetUpdateIntervalFrames)
            {
                // Decay old focus target
                teamFocusTarget = Vector2.Lerp(teamFocusTarget, highestThreatPosition, 1f - focusTargetDecayRate);

                // If threat is significantly closer, update focus target
                float distanceToNewThreat = Vector2.Distance(teamFocusTarget, highestThreatPosition);
                if (distanceToNewThreat > 5f)  // New threat is far enough
                {
                    teamFocusTarget = highestThreatPosition;
                }

                framesSinceFocusUpdate = 0;
            }

            framesSinceFocusUpdate++;
        }

        /// <summary>
        /// Validate agent action decision for team coordination
        /// Returns adjusted action or original if coordination not possible
        /// </summary>
        public int ValidateAction(
            int agentId,
            int originalAction,
            Vector2 agentPosition,
            AgentRole agentRole,
            Dictionary<int, Vector2> allAgentPositions,
            List<Vector2> enemyPositions)
        {
            // Get or create metrics for this agent
            if (!agentMetrics.ContainsKey(agentId))
            {
                agentMetrics[agentId] = new CoordinationMetrics();
            }

            int validatedAction = originalAction;

            // Apply role-specific validation
            switch (agentRole)
            {
                case AgentRole.Tank:
                    validatedAction = ValidateTankAction(agentId, originalAction, agentPosition, enemyPositions);
                    break;

                case AgentRole.DPS:
                    validatedAction = ValidateDPSAction(agentId, originalAction, agentPosition, enemyPositions);
                    break;

                case AgentRole.Support:
                    validatedAction = ValidateSupportAction(agentId, originalAction, agentPosition, allAgentPositions);
                    break;
            }

            return validatedAction;
        }

        /// <summary>
        /// Tank should hold aggro on focus target
        /// </summary>
        private int ValidateTankAction(int agentId, int action, Vector2 position, List<Vector2> enemies)
        {
            if (teamFocusTarget == Vector2.zero)
                return action;

            // Tank should move toward focus target or hold position
            float distanceToFocus = Vector2.Distance(position, teamFocusTarget);

            if (distanceToFocus > 3f)  // Too far from focus target
            {
                // Suggest moving closer (depends on action space encoding)
                // This is pseudo-code; actual implementation depends on ActionSpace
                return ModifyActionTowardTarget(action, position, teamFocusTarget);
            }

            return action;  // Tank is well-positioned, keep action
        }

        /// <summary>
        /// DPS should attack focus target, support tank's aggro
        /// </summary>
        private int ValidateDPSAction(int agentId, int action, Vector2 position, List<Vector2> enemies)
        {
            if (teamFocusTarget == Vector2.zero)
                return action;

            // Check if action targets focus target
            Vector2 actionTarget = GetActionTarget(action, position);
            float distanceToFocus = Vector2.Distance(actionTarget, teamFocusTarget);

            if (distanceToFocus < 2f)  // Already on focus target
            {
                return action;  // Good, keep it
            }

            // Suggest switching to focus target attack
            return ModifyActionTowardTarget(action, position, teamFocusTarget);
        }

        /// <summary>
        /// Support should protect low-health allies and stay near tank
        /// </summary>
        private int ValidateSupportAction(int agentId, int action, Vector2 position, Dictionary<int, Vector2> allAgentPositions)
        {
            // Find low-health ally (highest need for support)
            int lowestHealthAlly = FindLowestHealthAlly(allAgentPositions);

            if (lowestHealthAlly >= 0 && allAgentPositions.TryGetValue(lowestHealthAlly, out var allyPos))
            {
                float distanceToAlly = Vector2.Distance(position, allyPos);

                if (distanceToAlly > 3f)  // Too far from ally who needs help
                {
                    // Suggest moving closer to ally
                    return ModifyActionTowardTarget(action, position, allyPos);
                }
            }

            // Also stay close to tank for coordinated defense
            if (tankAgentId >= 0 && allAgentPositions.TryGetValue(tankAgentId, out var tankPos))
            {
                float distanceToTank = Vector2.Distance(position, tankPos);

                if (distanceToTank > 5f)  // Too far from tank
                {
                    return ModifyActionTowardTarget(action, position, tankPos);
                }
            }

            return action;
        }

        /// <summary>
        /// Calculate coordination metrics for team
        /// </summary>
        public CoordinationMetrics CalculateTeamMetrics(
            Dictionary<int, int> agentActions,
            Dictionary<int, Vector2> agentPositions,
            Vector2 focusTarget)
        {
            var metrics = new CoordinationMetrics();

            if (agentActions.Count == 0)
                return metrics;

            // Focus fire accuracy
            int agentsOnFocus = 0;
            foreach (var (agentId, action) in agentActions)
            {
                if (agentPositions.TryGetValue(agentId, out var pos))
                {
                    Vector2 actionTarget = GetActionTarget(action, pos);
                    float distanceToFocus = Vector2.Distance(actionTarget, focusTarget);

                    if (distanceToFocus < 2f)
                        agentsOnFocus++;
                }
            }

            metrics.focusFireAccuracy = (float)agentsOnFocus / agentActions.Count;

            // Formation score (check spread)
            if (agentPositions.Count > 1)
            {
                float avgSpread = CalculateAverageSpread(agentPositions.Values);
                float spreadDeviation = Mathf.Abs(avgSpread - optimalFormationSpread);
                metrics.formationScore = Mathf.Clamp01(1f - (spreadDeviation / optimalFormationSpread));
            }
            else
            {
                metrics.formationScore = 1f;
            }

            // Aggro distribution (should be on tank)
            int damageOnTank = 0;
            int totalDamage = agentActions.Count;

            foreach (var (agentId, action) in agentActions)
            {
                if (agentId == tankAgentId)
                    damageOnTank++;
            }

            metrics.aggroDistribution = (float)damageOnTank / totalDamage;

            // Overall coordination bonus
            metrics.coordinationBonus = 1f;

            if (metrics.focusFireAccuracy >= focusFireThreshold)
                metrics.coordinationBonus *= focusFireBonus;

            if (metrics.formationScore > 0.7f)
                metrics.coordinationBonus *= formationBonus;

            if (metrics.aggroDistribution > 0.6f)  // Most damage on tank
                metrics.coordinationBonus *= aggroShareBonus;

            return metrics;
        }

        /// <summary>
        /// Get team coordination bonus for reward shaping
        /// </summary>
        public float GetTeamCoordinationBonus(CoordinationMetrics metrics)
        {
            return metrics.coordinationBonus - 1f;  // Return multiplier delta
        }

        // Helper methods

        private int FindLowestHealthAlly(Dictionary<int, Vector2> allAgents)
        {
            // This would need agent health tracking
            // For now, just return -1 (no ally tracking yet)
            return -1;
        }

        private Vector2 GetActionTarget(int action, Vector2 position)
        {
            // Decode action to get target position
            // This is pseudo-code; actual implementation depends on ActionSpace
            // Could be relative offset, angle+distance, or entity reference

            // Example: if action is angle-based
            float angle = (action * 360f) / 8f;  // 8 directions
            float distance = 5f;  // Typical attack range

            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return position + direction * distance;
        }

        private int ModifyActionTowardTarget(int action, Vector2 from, Vector2 to)
        {
            // Modify action to move/attack toward target
            // This is pseudo-code; actual implementation depends on ActionSpace

            Vector2 direction = (to - from).normalized;
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Convert angle to action index (assumes 8 directions)
            int actionIndex = Mathf.RoundToInt(targetAngle / 45f) % 8;
            if (actionIndex < 0) actionIndex += 8;

            return actionIndex;
        }

        private float CalculateAverageSpread(ICollection<Vector2> positions)
        {
            if (positions.Count < 2)
                return 0f;

            float totalDistance = 0f;
            int count = 0;

            var posArray = new List<Vector2>(positions);
            for (int i = 0; i < posArray.Count; i++)
            {
                for (int j = i + 1; j < posArray.Count; j++)
                {
                    totalDistance += Vector2.Distance(posArray[i], posArray[j]);
                    count++;
                }
            }

            return totalDistance / count;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CooperativeValidator (Tank={tankAgentId})");
            sb.AppendLine($"  Focus Target: {teamFocusTarget}");
            sb.AppendLine($"  Formation Spread: {optimalFormationSpread}");
            sb.AppendLine($"  Tracked Agents: {agentMetrics.Count}");
            return sb.ToString();
        }
    }
}
```

### Step 2: Integrate with RLEnvironment

```csharp
// In RLEnvironment.cs

private CooperativeDecisionValidator cooperativeValidator;

public void InitializeCooperativeValidator(int tankId, List<int> supportIds, List<int> dpsIds)
{
    cooperativeValidator = new CooperativeDecisionValidator();
    cooperativeValidator.Initialize(tankId, supportIds, dpsIds);
}

/// <summary>
/// Get validated action considering team coordination
/// </summary>
public int ValidateAgentAction(
    int agentId,
    int originalAction,
    Vector2 agentPosition,
    AgentRole agentRole)
{
    if (cooperativeValidator == null)
        return originalAction;

    // Get all agent positions and actions
    var allPositions = new Dictionary<int, Vector2>();
    var enemyPositions = new List<Vector2>();

    // Populate from current game state...
    // (Implementation depends on EntityManager)

    return cooperativeValidator.ValidateAction(
        agentId,
        originalAction,
        agentPosition,
        agentRole,
        allPositions,
        enemyPositions
    );
}

/// <summary>
/// Calculate team coordination reward bonus
/// </summary>
public float GetCoordinationBonus(Dictionary<int, int> agentActions, Dictionary<int, Vector2> agentPositions)
{
    if (cooperativeValidator == null)
        return 0f;

    var metrics = cooperativeValidator.CalculateTeamMetrics(
        agentActions,
        agentPositions,
        Vector2.zero  // Would get actual focus target
    );

    return cooperativeValidator.GetTeamCoordinationBonus(metrics);
}
```

### Step 3: Apply Coordination Rewards

```csharp
// In RewardCalculator.cs

[SerializeField] private float coordinationBonusMultiplier = 0.2f;

public float CalculateReward(...)
{
    float baseReward = CalculateDamageReward() + CalculateSurvivalReward() + ...;

    // Get coordination bonus from validator
    float coordinationBonus = environment.GetCoordinationBonus(...);

    float totalReward = baseReward + (coordinationBonus * coordinationBonusMultiplier);

    return totalReward;
}
```

## Monitoring & Logging

```csharp
// In PerformanceMonitor or MetricsLogger

public void LogCoordinationMetrics(CoordinationMetrics metrics)
{
    Debug.Log($"[Coordination] Focus Fire: {metrics.focusFireAccuracy:P1}, " +
              $"Formation: {metrics.formationScore:F2}, " +
              $"Aggro: {metrics.aggroDistribution:P1}, " +
              $"Bonus: {metrics.coordinationBonus:F2}x");
}
```

## Testing

```csharp
[Test]
public void TestCooperativeValidation()
{
    var validator = new CooperativeDecisionValidator();
    validator.Initialize(tankId: 0, supportIds: new List<int> { 1 }, dpsIds: new List<int> { 2, 3 });

    // Test focus fire validation
    int dpsAction = validator.ValidateAction(2, originalAction: 5, position: new Vector2(0, 0), AgentRole.DPS, allPositions, enemies);
    Assert.AreNotEqual(5, dpsAction);  // Action should be modified toward focus

    // Test tank validation
    int tankAction = validator.ValidateAction(0, originalAction: 2, position: new Vector2(10, 10), AgentRole.Tank, allPositions, enemies);
    Assert.AreNotEqual(2, tankAction);  // Tank should move toward focus
}
```

## Benefits

1. **Emergent Coordination**: Team learns coordinated behaviors naturally
2. **Role-based Strategies**: Tank holds aggro, DPS focuses fire, Support protects
3. **Reward Shaping**: Bonuses for cooperation incentivize team play
4. **Monitoring**: Metrics show how well team is coordinating
5. **Debugging**: Easy to identify coordination failures and causes

## Next Steps

1. Implement actual action decoding based on your ActionSpace
2. Add health tracking for finding low-health allies
3. Implement focus target decay and threat assessment
4. Add more sophisticated formation validation (angle-based)
5. Create dashboard for monitoring coordination metrics

## References

- [MULTI_AGENT_IMPLEMENTATION_AUDIT.md](MULTI_AGENT_IMPLEMENTATION_AUDIT.md)
- [COOP_REWARD_SYSTEM.md](COOP_REWARD_SYSTEM.md)
- [AGENT_ID_BATCHING_GUIDE.md](AGENT_ID_BATCHING_GUIDE.md)
