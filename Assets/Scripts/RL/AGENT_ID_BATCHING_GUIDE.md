# Agent ID-based Decision Grouping Implementation Guide

## Overview

This guide explains how to implement agent ID-based decision grouping for multi-agent RL coordination in Co-op Survivors RL. This enables grouped inference for better performance and coordinated team behaviors.

## Current Architecture

```
RLMonster (agent_id=0)     RLMonster (agent_id=1)
         ↓                            ↓
    StateEncoder                StateEncoder
         ↓                            ↓
    [90-float obs]            [90-float obs]
         ↓                            ↓
    InferenceBatcher ────────────────┘
         ↓
    [Batch of 2 observations]
         ↓
    Policy Network (batch forward)
         ↓
    [2 action outputs]
```

## Problem

Current batching groups agents by time interval, not by logical grouping:

- All agents waiting for 100ms batch together
- No consideration for agent roles (tank, DPS, support)
- No spatial locality optimization
- Team coordination harder to enforce

## Solution: Role-based Agent Grouping

```
Tank agents                        DPS agents                      Support agents
(agent_id: 0,2,4...)    ┐         (agent_id: 1,3,5...)    ┐      (agent_id: 6,7...)
                         │                                 │
                    Group A                            Group B                Group C
                    (Tank Policy)                      (DPS Policy)          (Support Policy)
                         │                                 │
                    Batch Inference 1           Batch Inference 2       Batch Inference 3
```

## Implementation Steps

### Step 1: Create Agent Role System

```csharp
// File: Assets/Scripts/RL/Core/AgentRole.cs

namespace Vampire.RL
{
    /// <summary>
    /// Agent roles for grouped decision-making and specialized policies
    /// </summary>
    public enum AgentRole
    {
        None = 0,
        Tank = 1,
        DPS = 2,
        Support = 3,
    }

    /// <summary>
    /// Configuration for agent role assignment
    /// </summary>
    [System.Serializable]
    public class RoleConfig
    {
        public AgentRole role;
        public int maxAgentsPerRole = 20;
        public string policyModelPath;
        public float rewardMultiplier = 1f;
        public int decisionPriority = 0; // Higher = processed first
    }
}
```

### Step 2: Create Agent Batch Group Class

```csharp
// File: Assets/Scripts/RL/Core/AgentBatchGroup.cs

using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Groups agents by role for coordinated batching and inference
    /// Handles agent registration, observation grouping, and action distribution
    /// </summary>
    public class AgentBatchGroup
    {
        public AgentRole Role { get; private set; }
        public int MaxAgents { get; private set; }

        private readonly List<int> agentIds = new List<int>();
        private readonly Dictionary<int, float[]> observations = new Dictionary<int, float[]>();
        private readonly Dictionary<int, int> actionsByAgentId = new Dictionary<int, int>();

        public int ActiveAgentCount => agentIds.Count;
        public IReadOnlyList<int> ActiveAgentIds => agentIds.AsReadOnly();

        public AgentBatchGroup(AgentRole role, int maxAgents)
        {
            Role = role;
            MaxAgents = maxAgents;
        }

        /// <summary>
        /// Register an agent in this group
        /// </summary>
        public bool TryAddAgent(int agentId)
        {
            if (agentIds.Contains(agentId))
                return false;

            if (agentIds.Count >= MaxAgents)
                return false;

            agentIds.Add(agentId);
            return true;
        }

        /// <summary>
        /// Remove an agent from this group
        /// </summary>
        public bool RemoveAgent(int agentId)
        {
            if (!agentIds.Remove(agentId))
                return false;

            observations.Remove(agentId);
            actionsByAgentId.Remove(agentId);
            return true;
        }

        /// <summary>
        /// Store observation for agent
        /// </summary>
        public void StoreObservation(int agentId, float[] observation)
        {
            if (!agentIds.Contains(agentId))
            {
                Debug.LogWarning($"[AgentBatchGroup] Agent {agentId} not in group {Role}");
                return;
            }

            observations[agentId] = observation;
        }

        /// <summary>
        /// Get grouped observations for batch inference
        /// </summary>
        public float[][] GetGroupedObservations()
        {
            var grouped = new float[agentIds.Count][];

            for (int i = 0; i < agentIds.Count; i++)
            {
                int agentId = agentIds[i];
                if (observations.TryGetValue(agentId, out var obs))
                {
                    grouped[i] = obs;
                }
                else
                {
                    Debug.LogWarning($"[AgentBatchGroup] Missing observation for agent {agentId}");
                    grouped[i] = new float[90]; // Empty observation
                }
            }

            return grouped;
        }

        /// <summary>
        /// Store actions from batch inference
        /// </summary>
        public void StoreActions(int[] actions)
        {
            if (actions.Length != agentIds.Count)
            {
                Debug.LogError($"[AgentBatchGroup] Action count mismatch: got {actions.Length}, expected {agentIds.Count}");
                return;
            }

            for (int i = 0; i < agentIds.Count; i++)
            {
                actionsByAgentId[agentIds[i]] = actions[i];
            }
        }

        /// <summary>
        /// Get action for specific agent
        /// </summary>
        public int? GetAction(int agentId)
        {
            return actionsByAgentId.TryGetValue(agentId, out var action) ? action : null;
        }

        /// <summary>
        /// Get all actions as dictionary
        /// </summary>
        public Dictionary<int, int> GetAllActions() => new Dictionary<int, int>(actionsByAgentId);

        /// <summary>
        /// Clear actions after distribution
        /// </summary>
        public void ClearActions()
        {
            actionsByAgentId.Clear();
            observations.Clear();
        }

        public override string ToString()
        {
            return $"AgentBatchGroup({Role}, {ActiveAgentCount}/{MaxAgents} agents)";
        }
    }
}
```

### Step 3: Create Agent Group Manager

```csharp
// File: Assets/Scripts/RL/Core/AgentGroupManager.cs

using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL
{
    /// <summary>
    /// Manages agent grouping by role and coordinates multi-agent decisions
    /// </summary>
    public class AgentGroupManager
    {
        private readonly Dictionary<AgentRole, AgentBatchGroup> groupsByRole = new Dictionary<AgentRole, AgentBatchGroup>();
        private readonly Dictionary<int, AgentRole> roleByAgentId = new Dictionary<int, AgentRole>();

        private int nextAgentId = 0;

        /// <summary>
        /// Initialize group manager with role configurations
        /// </summary>
        public void Initialize(RoleConfig[] configs)
        {
            foreach (var config in configs)
            {
                if (config.role == AgentRole.None) continue;

                var group = new AgentBatchGroup(config.role, config.maxAgentsPerRole);
                groupsByRole[config.role] = group;

                Debug.Log($"[AgentGroupManager] Created group for {config.role} (max {config.maxAgentsPerRole} agents)");
            }
        }

        /// <summary>
        /// Register a new agent with assigned role
        /// </summary>
        public int RegisterAgent(AgentRole role)
        {
            if (!groupsByRole.ContainsKey(role))
            {
                Debug.LogError($"[AgentGroupManager] No group configured for role {role}");
                return -1;
            }

            var group = groupsByRole[role];
            if (!group.TryAddAgent(nextAgentId))
            {
                Debug.LogWarning($"[AgentGroupManager] Cannot add agent to group {role} (full)");
                return -1;
            }

            roleByAgentId[nextAgentId] = role;
            int agentId = nextAgentId;
            nextAgentId++;

            Debug.Log($"[AgentGroupManager] Registered agent {agentId} with role {role}");
            return agentId;
        }

        /// <summary>
        /// Unregister an agent
        /// </summary>
        public void UnregisterAgent(int agentId)
        {
            if (!roleByAgentId.TryGetValue(agentId, out var role))
            {
                Debug.LogWarning($"[AgentGroupManager] Agent {agentId} not found");
                return;
            }

            if (groupsByRole.TryGetValue(role, out var group))
            {
                group.RemoveAgent(agentId);
            }

            roleByAgentId.Remove(agentId);
            Debug.Log($"[AgentGroupManager] Unregistered agent {agentId}");
        }

        /// <summary>
        /// Get agent's assigned role
        /// </summary>
        public AgentRole GetAgentRole(int agentId)
        {
            return roleByAgentId.TryGetValue(agentId, out var role) ? role : AgentRole.None;
        }

        /// <summary>
        /// Get group for role
        /// </summary>
        public AgentBatchGroup GetGroup(AgentRole role)
        {
            return groupsByRole.TryGetValue(role, out var group) ? group : null;
        }

        /// <summary>
        /// Get all active groups (sorted by decision priority)
        /// </summary>
        public List<AgentBatchGroup> GetActiveGroups()
        {
            var result = new List<AgentBatchGroup>();
            foreach (var group in groupsByRole.Values)
            {
                if (group.ActiveAgentCount > 0)
                {
                    result.Add(group);
                }
            }

            // Sort by role priority (Tank → DPS → Support)
            result.Sort((a, b) => ((int)a.Role).CompareTo((int)b.Role));
            return result;
        }

        /// <summary>
        /// Get total active agent count
        /// </summary>
        public int TotalActiveAgents
        {
            get
            {
                int total = 0;
                foreach (var group in groupsByRole.Values)
                {
                    total += group.ActiveAgentCount;
                }
                return total;
            }
        }

        /// <summary>
        /// Store observation for agent in its group
        /// </summary>
        public void StoreObservation(int agentId, float[] observation)
        {
            var role = GetAgentRole(agentId);
            if (role == AgentRole.None)
            {
                Debug.LogWarning($"[AgentGroupManager] Agent {agentId} has no assigned role");
                return;
            }

            var group = GetGroup(role);
            if (group != null)
            {
                group.StoreObservation(agentId, observation);
            }
        }

        /// <summary>
        /// Get action for agent from its group
        /// </summary>
        public int? GetAction(int agentId)
        {
            var role = GetAgentRole(agentId);
            if (role == AgentRole.None)
                return null;

            var group = GetGroup(role);
            return group?.GetAction(agentId);
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"AgentGroupManager (Total agents: {TotalActiveAgents})");
            foreach (var group in GetActiveGroups())
            {
                sb.AppendLine($"  {group}");
            }
            return sb.ToString();
        }
    }
}
```

### Step 4: Integrate with RLSystem

Update [Assets/Scripts/RL/RLSystem.cs](Assets/Scripts/RL/RLSystem.cs):

```csharp
// Add to RLSystem class
private AgentGroupManager agentGroupManager;

// Add role configs to inspector
[Header("Multi-Agent Grouping")]
[SerializeField] private RoleConfig[] roleConfigs = new RoleConfig[]
{
    new RoleConfig { role = AgentRole.Tank, maxAgentsPerRole = 10, decisionPriority = 0 },
    new RoleConfig { role = AgentRole.DPS, maxAgentsPerRole = 20, decisionPriority = 1 },
    new RoleConfig { role = AgentRole.Support, maxAgentsPerRole = 10, decisionPriority = 2 },
};

private void InitializeAgentGroupManager()
{
    agentGroupManager = new AgentGroupManager();
    agentGroupManager.Initialize(roleConfigs);
    Debug.Log("[RLSystem] Agent group manager initialized");
}

/// <summary>
/// Register a new RL agent with role assignment
/// </summary>
public int RegisterAgent(AgentRole role = AgentRole.DPS)
{
    if (!IsEnabled)
        return -1;

    return agentGroupManager.RegisterAgent(role);
}

/// <summary>
/// Unregister an agent
/// </summary>
public void UnregisterAgent(int agentId)
{
    if (!IsEnabled)
        return;

    agentGroupManager.UnregisterAgent(agentId);
}

/// <summary>
/// Get all active agent groups for batching
/// </summary>
public List<AgentBatchGroup> GetActiveAgentGroups()
{
    return agentGroupManager?.GetActiveGroups() ?? new List<AgentBatchGroup>();
}
```

### Step 5: Update Monster Agent Registration

Update monster spawning to assign roles:

```csharp
// In MonsterSpawner or similar spawn logic
public GameObject SpawnMonster(MonsterType type, Vector3 position)
{
    var monster = base.SpawnMonster(type, position);

    if (RLSystem.Instance?.IsEnabled ?? false)
    {
        // Assign agent role based on monster type
        var agentRole = GetAgentRoleForMonsterType(type);
        int agentId = RLSystem.Instance.RegisterAgent(agentRole);

        // Store agent_id on monster for later retrieval
        var rlAgent = monster.GetComponent<ILearningAgent>();
        if (rlAgent != null)
        {
            rlAgent.SetAgentId(agentId);
        }

        Debug.Log($"[Spawn] Monster {type} assigned agent_id={agentId}, role={agentRole}");
    }

    return monster;
}

private AgentRole GetAgentRoleForMonsterType(MonsterType type)
{
    // Assign roles based on monster type
    return type switch
    {
        MonsterType.Skeleton => AgentRole.DPS,      // Weak attackers
        MonsterType.Bat => AgentRole.DPS,           // Ranged DPS
        MonsterType.Zombie => AgentRole.Tank,       // Tank (more health)
        MonsterType.Boss => AgentRole.Tank,         // Boss is tank
        _ => AgentRole.DPS
    };
}
```

## Usage Example

```csharp
// In gameplay
var groupManager = RLSystem.Instance.agentGroupManager;

// Get all agent groups for batching
var activeGroups = groupManager.GetActiveGroups();

foreach (var group in activeGroups)
{
    // Get observations for all agents in this role group
    var observations = group.GetGroupedObservations();

    // Run batch inference on policy network for this role
    var actions = policyNetwork[group.Role].BatchInference(observations);

    // Store actions back in group
    group.StoreActions(actions);

    // Distribute actions to individual agents
    foreach (int agentId in group.ActiveAgentIds)
    {
        int? action = group.GetAction(agentId);
        if (action.HasValue)
        {
            monster.ExecuteAction(action.Value);
        }
    }

    // Clear for next batch
    group.ClearActions();
}

Debug.Log(groupManager.ToString());
```

## Benefits

1. **Coordinated Decisions**: Agents of same role batch together, enabling role-based strategies
2. **Performance**: Single batch inference per role instead of per-agent
3. **Specialization**: Different policies per role (tank holds, DPS focuses, support heals)
4. **Scalability**: Supports 50+ agents with role-based limiting
5. **Monitoring**: Easy to track per-role performance metrics

## Configuration Examples

### Aggressive Team

```csharp
new RoleConfig { role = AgentRole.DPS, maxAgentsPerRole = 30, decisionPriority = 1 },
new RoleConfig { role = AgentRole.Tank, maxAgentsPerRole = 5, decisionPriority = 0 },
new RoleConfig { role = AgentRole.Support, maxAgentsPerRole = 5, decisionPriority = 2 },
```

### Balanced Team

```csharp
new RoleConfig { role = AgentRole.Tank, maxAgentsPerRole = 15, decisionPriority = 0 },
new RoleConfig { role = AgentRole.DPS, maxAgentsPerRole = 20, decisionPriority = 1 },
new RoleConfig { role = AgentRole.Support, maxAgentsPerRole = 15, decisionPriority = 2 },
```

### Defensive Team

```csharp
new RoleConfig { role = AgentRole.Tank, maxAgentsPerRole = 25, decisionPriority = 0 },
new RoleConfig { role = AgentRole.Support, maxAgentsPerRole = 20, decisionPriority = 2 },
new RoleConfig { role = AgentRole.DPS, maxAgentsPerRole = 5, decisionPriority = 1 },
```

## Testing

```csharp
// Unit test for agent grouping
[Test]
public void TestAgentGroupRegistration()
{
    var manager = new AgentGroupManager();
    manager.Initialize(new RoleConfig[]
    {
        new RoleConfig { role = AgentRole.Tank, maxAgentsPerRole = 5 },
        new RoleConfig { role = AgentRole.DPS, maxAgentsPerRole = 10 },
    });

    // Register agents
    int agent1 = manager.RegisterAgent(AgentRole.Tank);
    int agent2 = manager.RegisterAgent(AgentRole.DPS);
    int agent3 = manager.RegisterAgent(AgentRole.DPS);

    Assert.AreEqual(agent1, 0);
    Assert.AreEqual(agent2, 1);
    Assert.AreEqual(agent3, 2);

    // Verify grouping
    var tankGroup = manager.GetGroup(AgentRole.Tank);
    var dpsGroup = manager.GetGroup(AgentRole.DPS);

    Assert.AreEqual(tankGroup.ActiveAgentCount, 1);
    Assert.AreEqual(dpsGroup.ActiveAgentCount, 2);
}
```

## References

- [MULTI_AGENT_IMPLEMENTATION_AUDIT.md](MULTI_AGENT_IMPLEMENTATION_AUDIT.md) - Full audit
- [INFERENCE_COST_CONTROL.md](INFERENCE_COST_CONTROL.md) - Performance tuning
- [COOP_REWARD_SYSTEM.md](COOP_REWARD_SYSTEM.md) - Cooperative rewards
