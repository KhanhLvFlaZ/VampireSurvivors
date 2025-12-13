using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Monster that uses Deep Q-Network (DQN) reinforcement learning for decision making
    /// Extends Monster base class and implements ILearningAgent interface
    /// Integrates with the existing monster behavior system through action selection and execution
    /// </summary>
    public class RLMonster : Monster, ILearningAgent
    {
        [Header("RL Configuration")]
        [SerializeField] private bool enableRL = true;
        [SerializeField] private MonsterType rlMonsterType = MonsterType.Melee;

        [Header("DQN Learning Parameters")]
        [SerializeField] private float explorationRate = 0.1f;
        [SerializeField] private float learningRate = 0.001f;
        [SerializeField] private float discountFactor = 0.99f;
        [SerializeField] private int updateFrequency = 100;

        [Header("Performance")]
        [SerializeField] private float maxInferenceTime = 16f; // Max milliseconds per frame

        // RL System components
        private ILearningAgent learningAgent;
        private IActionDecoder actionDecoder;
        private RLEnvironment rlEnvironment;

        // State management
        private RLGameState currentState;
        private RLGameState previousState;
        private MonsterAction currentAction;
        private float actionStartTime;
        private bool isTrainingMode = false;
        private int updateCounter = 0;

        // Experience tracking for reward calculation
        private float timeAlive = 0f;
        private float timeSinceLastDamage = 0f;
        private float timeSinceLastAttack = 0f;
        private Vector2 positionAtActionStart;
        private float healthAtActionStart;

        // Action execution tracking
        private bool actionInProgress = false;
        private const float MIN_ACTION_INTERVAL = 0.1f;
        private float lastActionTime = 0f;

        public bool IsTraining
        {
            get => isTrainingMode;
            set => isTrainingMode = value;
        }
        public MonsterType RLMonsterType => rlMonsterType;

        /// <summary>
        /// Initialize the RL monster with DQN agent and environment
        /// </summary>
        public override void Init(EntityManager entityManager, Character playerCharacter)
        {
            base.Init(entityManager, playerCharacter);

            if (!enableRL)
            {
                Debug.LogWarning($"RL disabled for monster {gameObject.name}");
                return;
            }

            try
            {
                // Create or get learning agent
                InitializeLearningAgent();

                // Create action decoder
                InitializeActionDecoder();

                Debug.Log($"RLMonster initialized for {rlMonsterType} with {actionDecoder?.GetActionCount() ?? 0} actions");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to initialize RLMonster: {ex.Message}\n{ex.StackTrace}");
                enableRL = false;
            }
        }

        /// <summary>
        /// Set up the learning agent instance
        /// </summary>
        private void InitializeLearningAgent()
        {
            try
            {
                // Create DQN learning agent
                learningAgent = new DQNLearningAgent();

                // Configure the agent
                if (learningAgent is DQNLearningAgent dqnAgent)
                {
                    var actionSpace = MonsterRLConfig.CreateDefault(rlMonsterType).actionSpace;
                    learningAgent.Initialize(rlMonsterType, actionSpace);
                    isTrainingMode = true; // Start in training mode by default
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to initialize learning agent: {ex.Message}");
                // Fallback: create dummy agent that always returns action 0
                learningAgent = gameObject.AddComponent<DummyLearningAgent>();
                learningAgent.Initialize(rlMonsterType, MonsterRLConfig.CreateDefault(rlMonsterType).actionSpace);
            }
        }

        /// <summary>
        /// Set up the action decoder to convert DQN outputs to game actions
        /// </summary>
        private void InitializeActionDecoder()
        {
            var actionSpace = MonsterRLConfig.CreateDefault(rlMonsterType).actionSpace;
            actionDecoder = ActionDecoderFactory.CreateDecoder(rlMonsterType, actionSpace);

            if (actionDecoder == null)
            {
                Debug.LogError($"Failed to create action decoder for {rlMonsterType}");
                throw new System.InvalidOperationException("Action decoder initialization failed");
            }
        }

        /// <summary>
        /// Set the RL environment for state observation and reward calculation
        /// Called by RLSystem during initialization
        /// </summary>
        public void SetRLEnvironment(RLEnvironment environment)
        {
            this.rlEnvironment = environment;
        }

        /// <summary>
        /// Update the monster behavior using RL agent decision
        /// Called from Monster's Update/FixedUpdate cycle
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (!enableRL || learningAgent == null || !alive)
                return;

            try
            {
                // Update timers
                timeAlive += Time.deltaTime;
                timeSinceLastDamage += Time.deltaTime;
                timeSinceLastAttack += Time.deltaTime;

                // Get current state from environment
                if (rlEnvironment != null)
                {
                    UpdateCurrentState();
                }

                // Perform action selection and execution periodically
                if (Time.time - lastActionTime >= MIN_ACTION_INTERVAL)
                {
                    SelectAndExecuteAction();
                    lastActionTime = Time.time;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in RLMonster Update: {ex.Message}");
                // Fallback to safe movement toward player
                rb.linearVelocity = ((Vector2)playerCharacter.transform.position - rb.position).normalized *
                    monsterBlueprint.movespeed;
            }
        }

        /// <summary>
        /// Update the current state observations for RL decision making
        /// </summary>
        private void UpdateCurrentState()
        {
            if (rlEnvironment == null)
                return;

            // Get float array observations from environment
            float[] stateArray = rlEnvironment.GetState(this);

            // Update RLGameState with current information
            previousState = currentState;
            currentState = RLGameState.CreateDefault();
            currentState.timeAlive = timeAlive;
            currentState.timeSincePlayerDamage = timeSinceLastDamage;
            currentState.monsterPosition = rb.position;
            currentState.monsterHealth = currentHealth / monsterBlueprint.hp;
            currentState.playerPosition = playerCharacter.transform.position;
        }

        /// <summary>
        /// Select action using DQN and execute it
        /// </summary>
        private void SelectAndExecuteAction()
        {
            if (learningAgent == null || actionDecoder == null)
                return;

            try
            {
                // Select action from DQN agent
                int actionIndex = learningAgent.SelectAction(currentState, isTrainingMode);

                // Decode action to monster-specific behavior
                currentAction = actionDecoder.IndexToAction(actionIndex);

                // Execute the action
                ExecuteMonsterAction(currentAction);

                // Store for experience replay
                if (isTrainingMode)
                {
                    positionAtActionStart = rb.position;
                    healthAtActionStart = currentHealth;
                    actionStartTime = Time.time;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error selecting/executing action: {ex.Message}");
                // Safe fallback: move toward player
                currentAction = MonsterAction.CreateMovement(
                    ((Vector2)playerCharacter.transform.position - rb.position).normalized
                );
                ExecuteMonsterAction(currentAction);
            }
        }

        /// <summary>
        /// Execute a decoded MonsterAction
        /// </summary>
        private void ExecuteMonsterAction(MonsterAction action)
        {
            if (action.actionType == ActionType.Wait)
            {
                // No movement
                rb.linearVelocity *= 0.9f; // Decelerate
                return;
            }

            switch (action.actionType)
            {
                case ActionType.Move:
                    ExecuteMovement(action);
                    break;

                case ActionType.Attack:
                    ExecuteAttack(action);
                    break;

                case ActionType.Retreat:
                    ExecuteRetreat(action);
                    break;

                case ActionType.DefensiveStance:
                    ExecuteDefensiveStance(action);
                    break;

                case ActionType.SpecialAttack:
                    ExecuteSpecialAttack(action);
                    break;

                case ActionType.Coordinate:
                    ExecuteCoordinate(action);
                    break;

                case ActionType.Ambush:
                    ExecuteAmbush(action);
                    break;

                default:
                    // Fallback to movement toward player
                    ExecuteMovement(MonsterAction.CreateMovement(
                        ((Vector2)playerCharacter.transform.position - rb.position).normalized
                    ));
                    break;
            }
        }

        /// <summary>
        /// Execute movement action
        /// </summary>
        private void ExecuteMovement(MonsterAction action)
        {
            Vector2 moveDirection = action.direction.normalized;
            float moveSpeed = monsterBlueprint.movespeed * action.intensity;
            rb.linearVelocity = moveDirection * moveSpeed;
        }

        /// <summary>
        /// Execute attack action
        /// </summary>
        private void ExecuteAttack(MonsterAction action)
        {
            // Attack is handled by the base monster class
            // This is a placeholder for RL-specific attack logic
            timeSinceLastAttack = 0f;

            // Move slightly toward player while attacking
            Vector2 towardPlayer = ((Vector2)playerCharacter.transform.position - rb.position).normalized;
            rb.linearVelocity = towardPlayer * (monsterBlueprint.movespeed * 0.5f);
        }

        /// <summary>
        /// Execute retreat action
        /// </summary>
        private void ExecuteRetreat(MonsterAction action)
        {
            Vector2 retreatDirection = action.direction.normalized;
            rb.linearVelocity = retreatDirection * (monsterBlueprint.movespeed * 1.2f); // Faster retreat
        }

        /// <summary>
        /// Execute defensive stance action
        /// </summary>
        private void ExecuteDefensiveStance(MonsterAction action)
        {
            // Reduce movement and prepare for incoming damage
            rb.linearVelocity *= 0.5f;
        }

        /// <summary>
        /// Execute special attack action (if monster supports it)
        /// </summary>
        private void ExecuteSpecialAttack(MonsterAction action)
        {
            // Special attacks are monster-type specific
            // Subclasses should override for specific behavior
            ExecuteAttack(action);
        }

        /// <summary>
        /// Execute coordination action with nearby monsters
        /// </summary>
        private void ExecuteCoordinate(MonsterAction action)
        {
            // Coordination with nearby monsters
            // Move to flanking position relative to player and other monsters
            Vector2 towardPlayer = ((Vector2)playerCharacter.transform.position - rb.position).normalized;
            // Move perpendicular to approach for flanking
            Vector2 flankDirection = new Vector2(-towardPlayer.y, towardPlayer.x);
            rb.linearVelocity = flankDirection * (monsterBlueprint.movespeed * 0.8f);
        }

        /// <summary>
        /// Execute ambush action
        /// </summary>
        private void ExecuteAmbush(MonsterAction action)
        {
            // Ambush: fast movement in a specific direction to surround player
            Vector2 ambushDirection = action.direction.normalized;
            rb.linearVelocity = ambushDirection * (monsterBlueprint.movespeed * 1.3f);
        }

        /// <summary>
        /// Initialize the agent with monster type and action space (ILearningAgent)
        /// </summary>
        public void Initialize(MonsterType monsterType, ActionSpace actionSpace)
        {
            this.rlMonsterType = monsterType;
            if (learningAgent != null)
            {
                learningAgent.Initialize(monsterType, actionSpace);
            }
        }

        /// <summary>
        /// Select an action based on game state (ILearningAgent)
        /// </summary>
        public int SelectAction(RLGameState state, bool isTraining)
        {
            if (learningAgent == null)
                return 0;

            return learningAgent.SelectAction(state, isTraining);
        }

        /// <summary>
        /// Store experience for training (ILearningAgent)
        /// </summary>
        public void StoreExperience(RLGameState state, int action, float reward, RLGameState nextState, bool done)
        {
            if (!isTrainingMode || learningAgent == null)
                return;

            learningAgent.StoreExperience(state, action, reward, nextState, done);

            // Also store to global ExperienceManager for centralized training
            var experience = new Experience
            {
                state = state,
                action = action,
                reward = reward,
                nextState = nextState,
                done = done
            };

            ExperienceManager.Instance.StoreExperience(experience);
        }

        /// <summary>
        /// Update agent policy (ILearningAgent)
        /// </summary>
        public void UpdatePolicy()
        {
            if (!isTrainingMode || learningAgent == null)
                return;

            updateCounter++;
            if (updateCounter % updateFrequency == 0)
            {
                learningAgent.UpdatePolicy();
            }
        }

        /// <summary>
        /// Save behavior profile (ILearningAgent)
        /// </summary>
        public void SaveBehaviorProfile(string filePath)
        {
            if (learningAgent is DQNLearningAgent dqnAgent)
            {
                dqnAgent.SaveBehaviorProfile(filePath);
            }
        }

        /// <summary>
        /// Load behavior profile (ILearningAgent)
        /// </summary>
        public void LoadBehaviorProfile(string filePath)
        {
            if (learningAgent is DQNLearningAgent dqnAgent)
            {
                dqnAgent.LoadBehaviorProfile(filePath);
            }
        }

        /// <summary>
        /// Get current learning metrics
        /// </summary>
        public LearningMetrics GetMetrics()
        {
            if (learningAgent is DQNLearningAgent dqnAgent)
            {
                return dqnAgent.GetMetrics();
            }
            return LearningMetrics.CreateDefault();
        }

        /// <summary>
        /// Override TakeDamage to track damage for reward calculation
        /// </summary>
        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            base.TakeDamage(damage, knockback);

            if (alive)
            {
                timeSinceLastDamage = 0f;

                // Store damage experience if training
                if (isTrainingMode && learningAgent != null && rlEnvironment != null)
                {
                    // Calculate damage penalty
                    float damageReward = -damage / monsterBlueprint.hp; // Normalize by max health
                    StoreExperience(
                        previousState,
                        updateCounter,
                        damageReward,
                        currentState,
                        false
                    );
                }
            }
        }

        /// <summary>
        /// Clean up when monster is killed
        /// </summary>
        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            // Calculate final episode reward
            if (isTrainingMode && learningAgent != null && rlEnvironment != null)
            {
                float survivalReward = timeAlive / 100f; // Reward for survival duration
                float episodeDoneReward = killedByPlayer ? -1f : 0.1f; // Penalty or small reward based on outcome

                StoreExperience(
                    previousState,
                    updateCounter,
                    survivalReward + episodeDoneReward,
                    currentState,
                    true // Episode is done
                );

                // Update policy with final experience
                UpdatePolicy();
            }

            yield return base.Killed(killedByPlayer);
        }
    }

    /// <summary>
    /// Dummy learning agent that returns action 0 (used as fallback)
    /// </summary>
    public class DummyLearningAgent : MonoBehaviour, ILearningAgent
    {
        public bool IsTraining { get; set; }

        public void Initialize(MonsterType monsterType, ActionSpace actionSpace) { }
        public int SelectAction(RLGameState state, bool isTraining) => 0;
        public void StoreExperience(RLGameState state, int action, float reward, RLGameState nextState, bool done) { }
        public void UpdatePolicy() { }
        public void SaveBehaviorProfile(string filePath) { }
        public void LoadBehaviorProfile(string filePath) { }
        public LearningMetrics GetMetrics() => LearningMetrics.CreateDefault();
    }
}
