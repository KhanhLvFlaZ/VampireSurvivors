using UnityEngine;
using System;
using System.Collections.Generic;

namespace Vampire.RL
{
    /// <summary>
    /// Central error handling system for RL component failures
    /// Provides error logging, recovery strategies, and graceful degradation
    /// Requirements: 5.1 (error handling), 5.2 (fallback mechanisms)
    /// </summary>
    public class RLErrorHandler : MonoBehaviour
    {
        private static RLErrorHandler instance;
        public static RLErrorHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    var obj = new GameObject("RLErrorHandler");
                    instance = obj.AddComponent<RLErrorHandler>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }

        [SerializeField] private bool logToFile = false;
        [SerializeField] private string logFilePath = "Assets/Logs/RLErrors.log";
        [SerializeField] private int maxLogSize = 1000;

        // Error tracking
        private List<RLError> errorHistory = new List<RLError>();
        private Dictionary<ErrorType, int> errorCounts = new Dictionary<ErrorType, int>();
        private Dictionary<object, int> componentErrorCounts = new Dictionary<object, int>();

        // Callbacks
        public delegate void ErrorOccurredHandler(RLError error);
        public event ErrorOccurredHandler OnErrorOccurred;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        /// <summary>
        /// Handle an RL error with recovery strategy
        /// Requirements: 5.1
        /// </summary>
        public void HandleError(ErrorType errorType, object source, string message, Exception exception = null)
        {
            // Create error object
            var error = new RLError
            {
                errorType = errorType,
                source = source,
                sourceType = source?.GetType().Name ?? "Unknown",
                message = message,
                exception = exception,
                timestamp = Time.time,
                stackTrace = exception?.StackTrace ?? StackTraceUtility.ExtractStackTrace()
            };

            // Log error
            LogError(error);

            // Track error
            TrackError(error);

            // Invoke callback
            OnErrorOccurred?.Invoke(error);

            // Get recovery strategy
            var recovery = GetRecoveryStrategy(error);

            // Apply recovery
            ApplyRecovery(error, recovery);
        }

        /// <summary>
        /// Log error to console and file
        /// </summary>
        private void LogError(RLError error)
        {
            string logMessage = $"[{error.timestamp:F2}s] {error.errorType}: {error.message}";

            switch (error.errorType)
            {
                case ErrorType.Critical:
                    Debug.LogError($"{logMessage}\nSource: {error.sourceType}\n{error.stackTrace}");
                    break;
                case ErrorType.Warning:
                    Debug.LogWarning(logMessage);
                    break;
                case ErrorType.Info:
                    Debug.Log(logMessage);
                    break;
                default:
                    Debug.Log(logMessage);
                    break;
            }

            if (logToFile)
            {
                LogToFile(logMessage);
            }
        }

        /// <summary>
        /// Track error for analytics
        /// </summary>
        private void TrackError(RLError error)
        {
            // Add to history
            errorHistory.Add(error);
            if (errorHistory.Count > maxLogSize)
            {
                errorHistory.RemoveAt(0);
            }

            // Update error counts
            if (errorCounts.ContainsKey(error.errorType))
                errorCounts[error.errorType]++;
            else
                errorCounts[error.errorType] = 1;

            // Track per-component errors
            if (error.source != null)
            {
                if (componentErrorCounts.ContainsKey(error.source))
                    componentErrorCounts[error.source]++;
                else
                    componentErrorCounts[error.source] = 1;
            }
        }

        /// <summary>
        /// Get recovery strategy for error
        /// </summary>
        private RecoveryStrategy GetRecoveryStrategy(RLError error)
        {
            return error.errorType switch
            {
                ErrorType.Critical => RecoveryStrategy.DisableComponent,
                ErrorType.DataCorruption => RecoveryStrategy.ReinitializeComponent,
                ErrorType.InvalidConfiguration => RecoveryStrategy.UseDefaults,
                ErrorType.PerformanceIssue => RecoveryStrategy.ReduceQuality,
                ErrorType.TrainingFailure => RecoveryStrategy.PauseTraining,
                ErrorType.InferenceFailure => RecoveryStrategy.UseFallback,
                _ => RecoveryStrategy.LogAndContinue
            };
        }

        /// <summary>
        /// Apply recovery strategy
        /// Requirement: 5.2 - Fallback mechanisms
        /// </summary>
        private void ApplyRecovery(RLError error, RecoveryStrategy strategy)
        {
            switch (strategy)
            {
                case RecoveryStrategy.DisableComponent:
                    DisableRLComponent(error.source);
                    break;

                case RecoveryStrategy.ReinitializeComponent:
                    ReinitializeComponent(error.source);
                    break;

                case RecoveryStrategy.UseDefaults:
                    ApplyDefaultConfiguration(error.source);
                    break;

                case RecoveryStrategy.ReduceQuality:
                    ReduceQuality(error.source);
                    break;

                case RecoveryStrategy.PauseTraining:
                    PauseTraining(error.source);
                    break;

                case RecoveryStrategy.UseFallback:
                    ActivateFallback(error.source);
                    break;

                case RecoveryStrategy.LogAndContinue:
                default:
                    // No action needed
                    break;
            }
        }

        /// <summary>
        /// Disable RL component when critical error occurs
        /// </summary>
        private void DisableRLComponent(object source)
        {
            if (source is MonoBehaviour mb)
            {
                mb.enabled = false;
                Debug.LogWarning($"Disabled {mb.GetType().Name} due to critical error");
            }
        }

        /// <summary>
        /// Reinitialize component after data corruption
        /// </summary>
        private void ReinitializeComponent(object source)
        {
            if (source is IRecoverable recoverable)
            {
                recoverable.Recover();
            }
        }

        /// <summary>
        /// Apply default configuration
        /// </summary>
        private void ApplyDefaultConfiguration(object source)
        {
            if (source is IConfigurable configurable)
            {
                configurable.ApplyDefaults();
            }
        }

        /// <summary>
        /// Reduce quality for performance issues
        /// </summary>
        private void ReduceQuality(object source)
        {
            if (source is IQualityScalable scalable)
            {
                scalable.ReduceQuality();
            }
        }

        /// <summary>
        /// Pause training on failure
        /// </summary>
        private void PauseTraining(object source)
        {
            if (source is ITrainingManager trainer)
            {
                trainer.PauseTraining();
            }
        }

        /// <summary>
        /// Activate fallback behavior
        /// Requirement: 5.2
        /// </summary>
        private void ActivateFallback(object source)
        {
            if (source is IFallbackCapable fallbackCapable)
            {
                fallbackCapable.UseFallbackBehavior();
            }
        }

        /// <summary>
        /// Log error to file
        /// </summary>
        private void LogToFile(string message)
        {
            try
            {
                System.IO.File.AppendAllText(logFilePath, message + "\n");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to write to error log file: {ex.Message}");
            }
        }

        /// <summary>
        /// Get error statistics
        /// </summary>
        public RLErrorStatistics GetStatistics()
        {
            return new RLErrorStatistics
            {
                totalErrors = errorHistory.Count,
                errorsByType = new Dictionary<ErrorType, int>(errorCounts),
                recentErrors = errorHistory.Count > 5 ? errorHistory.GetRange(errorHistory.Count - 5, 5) : errorHistory,
                lastErrorTime = errorHistory.Count > 0 ? errorHistory[errorHistory.Count - 1].timestamp : 0f
            };
        }

        /// <summary>
        /// Clear error history
        /// </summary>
        public void ClearHistory()
        {
            errorHistory.Clear();
            errorCounts.Clear();
            componentErrorCounts.Clear();
        }

        /// <summary>
        /// Get component error count
        /// </summary>
        public int GetComponentErrorCount(object component)
        {
            return componentErrorCounts.TryGetValue(component, out int count) ? count : 0;
        }
    }

    /// <summary>
    /// Error type enumeration
    /// </summary>
    public enum ErrorType
    {
        Info,                    // Informational
        Warning,                 // Warning - non-critical
        Critical,                // Critical failure
        DataCorruption,          // Data is corrupted
        InvalidConfiguration,    // Configuration is invalid
        PerformanceIssue,       // Performance problem
        TrainingFailure,        // Training failed
        InferenceFailure,       // Inference failed
        FileIO,                 // File I/O error
        ModelLoad               // Model loading error
    }

    /// <summary>
    /// Recovery strategy enumeration
    /// </summary>
    public enum RecoveryStrategy
    {
        LogAndContinue,         // Log error and continue
        DisableComponent,       // Disable the component
        ReinitializeComponent,  // Reinitialize component
        UseDefaults,           // Apply default configuration
        ReduceQuality,         // Reduce quality/performance
        PauseTraining,         // Pause training
        UseFallback            // Use fallback behavior
    }

    /// <summary>
    /// Error information
    /// </summary>
    [System.Serializable]
    public class RLError
    {
        public ErrorType errorType;
        public object source;
        public string sourceType;
        public string message;
        public Exception exception;
        public float timestamp;
        public string stackTrace;
    }

    /// <summary>
    /// Interface for components that can recover from errors
    /// </summary>
    public interface IRecoverable
    {
        void Recover();
    }

    /// <summary>
    /// Interface for components with configurable defaults
    /// </summary>
    public interface IConfigurable
    {
        void ApplyDefaults();
    }

    /// <summary>
    /// Interface for components that support quality scaling
    /// </summary>
    public interface IQualityScalable
    {
        void ReduceQuality();
        void IncreaseQuality();
    }

    /// <summary>
    /// Interface for components that support training management
    /// </summary>
    public interface ITrainingManager
    {
        void PauseTraining();
        void ResumeTraining();
    }

    /// <summary>
    /// Interface for components with fallback behavior
    /// Requirement: 5.2 - Fallback mechanisms
    /// </summary>
    public interface IFallbackCapable
    {
        void UseFallbackBehavior();
        void ResumeNormalBehavior();
        bool IsFallbackActive { get; }
    }

    /// <summary>
    /// RL Error statistics data structure
    /// </summary>
    public class RLErrorStatistics
    {
        public int totalErrors;
        public Dictionary<ErrorType, int> errorsByType;
        public List<RLError> recentErrors;
        public float lastErrorTime;
    }
}

