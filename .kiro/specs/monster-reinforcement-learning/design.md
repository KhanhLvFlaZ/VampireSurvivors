# Monster Reinforcement Learning Design Document

## Overview

Hệ thống Monster Reinforcement Learning sẽ tích hợp khả năng học tăng cường vào các monster hiện có trong game Vampire Survivors. Hệ thống này sử dụng Deep Q-Network (DQN) với Unity ML-Agents để tạo ra monster có hành vi thông minh, thích ứng và có thể học từ gameplay patterns của người chơi.

Thiết kế tập trung vào việc tạo ra trải nghiệm rõ ràng cho người chơi thấy được sự khác biệt giữa AI truyền thống và AI học tăng cường thông qua visual indicators, coordinated behaviors, và adaptive strategies.

## Architecture

### Core Components

1. **RL System Manager**: Quản lý toàn bộ hệ thống RL, training và inference
2. **Monster RL Agent**: Component tích hợp vào monster để cung cấp khả năng RL
3. **Environment Simulator**: Mô phỏng môi trường game cho training
4. **Reward Calculator**: Tính toán reward dựa trên hành vi monster
5. **Behavior Visualizer**: Hiển thị visual indicators cho RL behaviors
6. **Model Manager**: Quản lý save/load trained models
7. **Performance Monitor**: Theo dõi performance và optimization

### Integration with Existing System

Hệ thống RL sẽ tích hợp với architecture hiện có:
- Extend từ Monster base class để tạo RLMonster
- Sử dụng EntityManager để spawn và manage RL monsters
- Tích hợp với MonsterBlueprint system để configure RL parameters
- Sử dụng SpatialHashGrid để optimize RL observations

## Components and Interfaces

### IRLAgent Interface
```csharp
public interface IRLAgent
{
    void Initialize(RLConfig config);
    int SelectAction(float[] observations);
    void StoreExperience(float[] state, int action, float reward, float[] nextState, bool done);
    void UpdateModel();
    float[] GetObservations();
}
```

### IRLEnvironment Interface
```csharp
public interface IRLEnvironment
{
    float[] GetState(Monster monster);
    float CalculateReward(Monster monster, int action, float[] previousState);
    bool IsEpisodeComplete(Monster monster);
    void ResetEnvironment();
}
```

### IBehaviorVisualizer Interface
```csharp
public interface IBehaviorVisualizer
{
    void ShowDecisionIndicator(Monster monster, int action, float confidence);
    void ShowCoordinationIndicator(List<Monster> monsters);
    void ShowAdaptationIndicator(Monster monster, string adaptationType);
    void ShowDebugInfo(Monster monster, float[] state, int action);
}
```

## Data Models

### RLConfig
```csharp
[System.Serializable]
public class RLConfig
{
    public int stateSize = 20;
    public int actionSize = 8;
    public float learningRate = 0.001f;
    public float discountFactor = 0.99f;
    public float explorationRate = 0.1f;
    public int memorySize = 10000;
    public int batchSize = 32;
    public bool useCoordination = true;
    public float coordinationWeight = 0.2f;
}
```

### RLState
```csharp
[System.Serializable]
public class RLState
{
    public Vector2 playerPosition;
    public Vector2 playerVelocity;
    public float playerHealth;
    public Vector2 monsterPosition;
    public Vector2 monsterVelocity;
    public float monsterHealth;
    public Vector2[] nearbyMonsterPositions;
    public Vector2[] nearbyObstacles;
    public float timeSinceLastAttack;
    public float distanceToPlayer;
    public int monstersInRange;
    public float[] ToArray() { /* Convert to float array */ }
}
```

### RLAction
```csharp
public enum RLAction
{
    MoveTowardPlayer = 0,
    MoveAwayFromPlayer = 1,
    MoveLeft = 2,
    MoveRight = 3,
    Attack = 4,
    Coordinate = 5,
    Flank = 6,
    Wait = 7
}
```

### RewardComponents
```csharp
[System.Serializable]
public class RewardComponents
{
    public float damageDealtReward = 10.0f;
    public float survivalReward = 0.1f;
    public float coordinationReward = 5.0f;
    public float positioningReward = 2.0f;
    public float deathPenalty = -50.0f;
    public float timeoutPenalty = -10.0f;
}
```

## 
Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Property 1: Agent Initialization Consistency
*For any* Monster_Agent spawn event, the RL_System should initialize the agent with properly sized state space and action space matching the configuration
**Validates: Requirements 1.1**

Property 2: State Vector Completeness
*For any* environment observation, the state vector should contain all required information including player position, health, nearby monsters, and obstacles
**Validates: Requirements 1.2**

Property 3: Action Execution Consistency
*For any* valid action selected by Monster_Agent, the RL_System should apply the action to monster behavior and update the reward accordingly
**Validates: Requirements 1.3**

Property 4: Experience Storage Completeness
*For any* completed episode, all experience data (state, action, reward, next_state, done) should be stored in the correct format for training
**Validates: Requirements 1.4**

Property 5: Multi-Agent Coordination
*For any* set of Monster_Agents in proximity, the coordination system should handle their interactions without conflicts
**Validates: Requirements 1.5**

Property 6: Training Loop Execution
*For any* training mode activation, the system should execute the training loop with proper environment simulation
**Validates: Requirements 2.1**

Property 7: Model Weight Updates
*For any* completed training episode with collected rewards, the neural network weights should be updated based on the learning algorithm
**Validates: Requirements 2.2**

Property 8: Metrics Reporting Accuracy
*For any* training progress request, the system should provide complete and accurate learning performance metrics
**Validates: Requirements 2.3**

Property 9: Model Persistence on Convergence
*For any* converged model, the system should save the trained model in a format suitable for inference mode
**Validates: Requirements 2.4**

Property 10: Configuration Update Handling
*For any* hyperparameter change, the training system should restart with the new configuration without data corruption
**Validates: Requirements 2.5**

Property 11: Decision Visualization
*For any* Monster_Agent decision, the Behavior_Visualizer should display appropriate visual indicators
**Validates: Requirements 3.1**

Property 12: Coordination Visualization
*For any* coordinated attack by multiple Monster_Agents, the visualizer should highlight the team behavior patterns
**Validates: Requirements 3.2**

Property 13: Adaptation Visualization
*For any* strategy adaptation by Monster_Agent, the visualizer should show clear adaptation indicators
**Validates: Requirements 3.3**

Property 14: Confidence Display
*For any* Monster_Agent under player observation, the confidence level of AI decisions should be displayed
**Validates: Requirements 3.4**

Property 15: Debug Information Display
*For any* debug mode activation, detailed state and action information should be shown for all Monster_Agents
**Validates: Requirements 3.5**

Property 16: Damage Reward Proportionality
*For any* damage dealt by Monster_Agent to player, the reward should be proportional to the damage amount
**Validates: Requirements 4.1**

Property 17: Death Penalty Calculation
*For any* Monster_Agent death, the negative reward should be calculated based on survival time
**Validates: Requirements 4.2**

Property 18: Cooperation Reward Assignment
*For any* cooperative behavior between Monster_Agents, appropriate rewards should be assigned to participating agents
**Validates: Requirements 4.3**

Property 19: Positioning Reward Calculation
*For any* tactical positioning by Monster_Agent, the reward system should evaluate and reward effective environment usage
**Validates: Requirements 4.4**

Property 20: Runtime Reward Configuration
*For any* reward parameter adjustment during runtime, the reward calculation should update immediately without requiring restart
**Validates: Requirements 4.5**

Property 21: Inference Performance Constraint
*For any* RL inference operation, the decision making should complete within 5ms per monster
**Validates: Requirements 5.1**

Property 22: Framerate Stability
*For any* number of active Monster_Agents, the system should maintain framerate above 60 FPS
**Validates: Requirements 5.2**

Property 23: Memory Usage Constraint
*For any* RL system operation, the total memory usage for RL components should not exceed 100MB
**Validates: Requirements 5.3**

Property 24: Model Size Constraint
*For any* optimized model, the file size should be under 10MB when quantized
**Validates: Requirements 5.4**

Property 25: Profiling Data Availability
*For any* detected performance bottleneck, the system should provide profiling data and optimization suggestions
**Validates: Requirements 5.5**

Property 26: Model Serialization Completeness
*For any* trained model save operation, both model weights and hyperparameters should be serialized to the file format
**Validates: Requirements 6.1**

Property 27: Model Load Consistency (Round-trip)
*For any* saved model, loading it should restore the exact same behavior as the original trained agent
**Validates: Requirements 6.2**

Property 28: Version Compatibility Validation
*For any* model version check, the system should validate compatibility with the current game version
**Validates: Requirements 6.3**

Property 29: Model Switching Capability
*For any* set of available models, the system should allow switching between different behavior models without errors
**Validates: Requirements 6.4**

Property 30: Metadata Query Completeness
*For any* model metadata query, the system should provide complete training statistics and performance metrics
**Validates: Requirements 6.5**

Property 31: Strategy Counter-Adaptation
*For any* specific player strategy, the RL system should detect and adapt monster behavior to counter that strategy
**Validates: Requirements 7.1**

Property 32: Movement Prediction Adjustment
*For any* analyzed player movement pattern, the system should adjust monster positioning to predict player moves
**Validates: Requirements 7.2**

Property 33: Difficulty Scaling Appropriateness
*For any* evaluated player skill level, the system should scale monster difficulty appropriately
**Validates: Requirements 7.3**

Property 34: Behavior Persistence After Adaptation
*For any* completed adaptation period, the learned behaviors should be maintained for subsequent encounters
**Validates: Requirements 7.4**

Property 35: Re-adaptation Timeliness
*For any* change in player behavior, the system should re-adapt within a reasonable time frame
**Validates: Requirements 7.5**

## Error Handling

### Training Errors
- **Model Convergence Failure**: Implement early stopping and hyperparameter adjustment
- **Memory Overflow**: Implement experience replay buffer management with size limits
- **Training Instability**: Implement gradient clipping and learning rate scheduling

### Runtime Errors
- **Inference Timeout**: Fallback to traditional AI behavior if RL inference takes too long
- **Model Loading Failure**: Graceful degradation to default monster behavior
- **Performance Degradation**: Automatic quality scaling and agent count reduction

### Data Errors
- **Corrupted Experience Data**: Validation and cleanup of experience replay buffer
- **Invalid State Observations**: Bounds checking and normalization of state vectors
- **Action Space Violations**: Constraint enforcement and action validation

## Testing Strategy

### Unit Testing
- Test individual RL components (agent initialization, action selection, reward calculation)
- Test data structures (state vectors, experience buffers, model serialization)
- Test integration points with existing monster system
- Test error handling and edge cases

### Property-Based Testing
- Use Unity Test Framework with custom generators for RL-specific data types
- Configure each property test to run minimum 100 iterations for statistical confidence
- Test universal properties that should hold across all inputs and scenarios
- Focus on correctness properties defined in this document

**Property-Based Testing Library**: Unity Test Framework with custom property generators
**Minimum Iterations**: 100 per property test
**Test Tagging Format**: '**Feature: monster-reinforcement-learning, Property {number}: {property_text}**'

### Integration Testing
- Test RL system integration with EntityManager and Monster spawning
- Test multi-agent coordination scenarios
- Test training and inference mode transitions
- Test model save/load workflows

### Performance Testing
- Benchmark inference time per monster under various loads
- Monitor memory usage during extended gameplay sessions
- Test framerate stability with multiple RL agents active
- Profile bottlenecks and optimization opportunities

### Visual Testing
- Verify behavior visualization indicators appear correctly
- Test debug mode information display
- Validate coordination and adaptation visual feedback
- Ensure visual elements don't interfere with gameplay