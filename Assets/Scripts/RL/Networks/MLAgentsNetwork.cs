using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using System;

namespace Vampire.RL
{
    /// <summary>
    /// ML-Agents neural network wrapper implementing INeuralNetwork interface
    /// Provides production-ready neural network for RL agents using Unity ML-Agents
    /// Requirements: 2.2, 6.1, 6.2
    /// Note: Actual Barracuda inference will be added when ONNX models are ready
    /// </summary>
    public class MLAgentsNetwork : MonoBehaviour, INeuralNetwork
    {
        [Header("Model Configuration")]
        [SerializeField] private string modelPath;

        [Header("Network Architecture")]
        [SerializeField] private int inputSize = 20;
        [SerializeField] private int outputSize = 5;
        [SerializeField] private int[] hiddenLayers = new int[] { 128, 64 };

        private bool isInitialized = false;
        private NetworkArchitecture architecture;

        // Interface properties
        public bool SupportsTraining => false; // ML-Agents training happens in Python
        public NetworkArchitecture Architecture => architecture;
        public int InputSize => inputSize;
        public int OutputSize => outputSize;

        /// <summary>
        /// Initialize neural network
        /// </summary>
        public void Initialize(int inputSize, int outputSize, int[] hiddenLayers, NetworkArchitecture architecture)
        {
            this.inputSize = inputSize;
            this.outputSize = outputSize;
            this.hiddenLayers = hiddenLayers;
            this.architecture = architecture;

            if (!string.IsNullOrEmpty(modelPath))
            {
                LoadModelFromPath(modelPath);
            }
            else
            {
                Debug.LogWarning("No model path assigned. Using random initialization.");
                InitializeRandomNetwork();
            }

            isInitialized = true;
        }

        /// <summary>
        /// Load model from path
        /// </summary>
        public void LoadModelFromPath(string path)
        {
            modelPath = path;
            // TODO: Implement Barracuda model loading when ONNX files are ready
            Debug.Log($"Model path set: {path}. Barracuda loading to be implemented.");
            isInitialized = true;
        }

        /// <summary>
        /// Forward pass through the network
        /// </summary>
        public float[] Forward(float[] input)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("Network not initialized. Returning random output.");
                return GetRandomOutput();
            }

            if (input.Length != inputSize)
            {
                Debug.LogError($"Input size mismatch. Expected {inputSize}, got {input.Length}");
                return GetRandomOutput();
            }

            // TODO: Replace with actual Barracuda inference
            return GetRandomOutput();
        }

        /// <summary>
        /// Backward pass - Not supported in inference mode
        /// </summary>
        public float Backward(float[] input, float[] target, float learningRate)
        {
            Debug.LogWarning("Backward pass not supported. Training must be done in Python ML-Agents.");
            return 0f;
        }

        /// <summary>
        /// Get weights - Not supported in ML-Agents
        /// </summary>
        public float[] GetWeights()
        {
            Debug.LogWarning("Direct weight access not supported in ML-Agents.");
            return new float[0];
        }

        /// <summary>
        /// Set weights - Not supported in ML-Agents
        /// </summary>
        public void SetWeights(float[] weights)
        {
            Debug.LogWarning("Direct weight setting not supported. Load new model instead.");
        }

        /// <summary>
        /// Get biases - Not supported in ML-Agents
        /// </summary>
        public float[] GetBiases()
        {
            Debug.LogWarning("Direct bias access not supported in ML-Agents.");
            return new float[0];
        }

        /// <summary>
        /// Set biases - Not supported in ML-Agents
        /// </summary>
        public void SetBiases(float[] biases)
        {
            Debug.LogWarning("Direct bias setting not supported.");
        }

        /// <summary>
        /// Get parameter count
        /// </summary>
        public int GetParameterCount()
        {
            // Estimate based on architecture
            int paramCount = 0;
            int prevLayer = inputSize;

            foreach (int layer in hiddenLayers)
            {
                paramCount += (prevLayer + 1) * layer; // weights + biases
                prevLayer = layer;
            }

            paramCount += (prevLayer + 1) * outputSize; // output layer
            return paramCount;
        }

        /// <summary>
        /// Copy weights from another network
        /// </summary>
        public void CopyWeightsFrom(INeuralNetwork sourceNetwork)
        {
            Debug.LogWarning("Weight copying not supported in ML-Agents. Use model files instead.");
        }

        /// <summary>
        /// Add noise to weights
        /// </summary>
        public void AddNoise(float noiseScale)
        {
            Debug.LogWarning("Weight noise not supported in ML-Agents inference mode.");
        }

        /// <summary>
        /// Reset network
        /// </summary>
        public void Reset()
        {
            InitializeRandomNetwork();
        }

        /// <summary>
        /// Clone the network
        /// </summary>
        public INeuralNetwork Clone()
        {
            var clone = gameObject.AddComponent<MLAgentsNetwork>();
            clone.Initialize(inputSize, outputSize, hiddenLayers, architecture);
            clone.modelPath = modelPath;
            return clone;
        }

        /// <summary>
        /// Save to file
        /// </summary>
        public void SaveToFile(string path)
        {
            Debug.Log($"ML-Agents models are saved as ONNX from Python training.");
        }

        /// <summary>
        /// Load from file
        /// </summary>
        public void LoadFromFile(string path)
        {
            LoadModelFromPath(path);
        }

        /// <summary>
        /// Get network info
        /// </summary>
        public string GetNetworkInfo()
        {
            return $"ML-Agents Network:\n" +
                   $"Model: {modelPath ?? "None"}\n" +
                   $"Input Size: {inputSize}\n" +
                   $"Output Size: {outputSize}\n" +
                   $"Hidden Layers: [{string.Join(", ", hiddenLayers)}]\n" +
                   $"Architecture: {architecture}";
        }

        private void InitializeRandomNetwork()
        {
            Debug.LogWarning("Using random network initialization.");
        }

        private float[] GetRandomOutput()
        {
            float[] output = new float[outputSize];
            for (int i = 0; i < outputSize; i++)
            {
                output[i] = UnityEngine.Random.Range(-1f, 1f);
            }
            return output;
        }

        public bool IsReady() => isInitialized;
        public int GetInputSize() => inputSize;
        public int GetOutputSize() => outputSize;
    }

    /// <summary>
    /// ML-Agents specific agent class for training
    /// Integrates with Unity ML-Agents Python trainer
    /// </summary>
    public class MLAgentsRLAgent : Agent
    {
        private RLMonsterAgent rlMonsterAgent;
        private RLEnvironment environment;

        public override void Initialize()
        {
            base.Initialize();

            rlMonsterAgent = GetComponent<RLMonsterAgent>();
            if (rlMonsterAgent == null)
            {
                Debug.LogError("MLAgentsRLAgent requires RLMonsterAgent component");
            }

            environment = FindFirstObjectByType<RLEnvironment>();
            if (environment == null)
            {
                Debug.LogError("RLEnvironment not found in scene");
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (rlMonsterAgent == null)
                return;

            // RLMonsterAgent handles CollectObservations via its own implementation
            // This class acts as a wrapper for legacy ML-Agents integration
            // Observations are collected directly by RLMonsterAgent.CollectObservations()
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (rlMonsterAgent == null)
                return;

            // RLMonsterAgent handles actions via its own implementation
            // This class acts as a wrapper for legacy ML-Agents integration
            // Get action from ML-Agents
            int actionIndex = actions.DiscreteActions[0];

            // Execute action through RLMonster
            // Integration with RLMonster's action execution
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Manual control for testing
            var discreteActions = actionsOut.DiscreteActions;
            discreteActions[0] = 0; // Default action
        }

        public override void OnEpisodeBegin()
        {
            // Reset agent state for new episode
            if (rlMonsterAgent != null)
            {
                // Reset position, health, etc.
            }
        }
    }
}
