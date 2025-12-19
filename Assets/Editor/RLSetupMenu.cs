#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Vampire;
using Vampire.RL;
using Vampire.RL.Integration;
using Vampire.RL.Training;

public static class RLSetupMenu
{
    [MenuItem("Vampire RL/Setup Custom RL Training")]
    public static void SetupCustomRLTraining()
    {
        var root = GameObject.Find("RL_Environment") ?? new GameObject("RL_Environment");

        // Add LevelRLIntegration host
        var integration = root.GetComponent<LevelRLIntegration>() ?? root.AddComponent<LevelRLIntegration>();

        // Create RLSystem node
        var rlSystemGO = GameObject.Find("RLSystem_Root") ?? new GameObject("RLSystem_Root");
        rlSystemGO.transform.SetParent(root.transform);
        var rlSystem = rlSystemGO.GetComponent<RLSystem>() ?? rlSystemGO.AddComponent<RLSystem>();

        // Try to auto-wire a Character if present
        var player = Object.FindFirstObjectByType<Character>();
        if (player != null)
        {
            // RLSystem takes the player reference at runtime via Initialize; here we just inform
            Debug.Log("RLSetup: Detected player Character in scene. RLSystem will bind on Initialize().");
        }
        else
        {
            Debug.LogWarning("RLSetup: No Character found. Assign your player before training.");
        }

        // Helpful utility components
        var perfGO = GameObject.Find("PerformanceMonitor") ?? new GameObject("PerformanceMonitor");
        perfGO.transform.SetParent(root.transform);
        if (perfGO.GetComponent<PerformanceMonitor>() == null) perfGO.AddComponent<PerformanceMonitor>();

        var metricsGO = GameObject.Find("TrainingMetricsLogger") ?? new GameObject("TrainingMetricsLogger");
        metricsGO.transform.SetParent(root.transform);
        if (metricsGO.GetComponent<TrainingMetricsLogger>() == null) metricsGO.AddComponent<TrainingMetricsLogger>();

        var checkpointsGO = GameObject.Find("CheckpointManager") ?? new GameObject("CheckpointManager");
        checkpointsGO.transform.SetParent(root.transform);
        if (checkpointsGO.GetComponent<CheckpointManager>() == null) checkpointsGO.AddComponent<CheckpointManager>();

        var trainingCtrlGO = GameObject.Find("TrainingController") ?? new GameObject("TrainingController");
        trainingCtrlGO.transform.SetParent(root.transform);
        if (trainingCtrlGO.GetComponent<TrainingController>() == null) trainingCtrlGO.AddComponent<TrainingController>();

        var evalGO = GameObject.Find("EvaluationScenarioManager") ?? new GameObject("EvaluationScenarioManager");
        evalGO.transform.SetParent(root.transform);
        if (evalGO.GetComponent<EvaluationScenarioManager>() == null) evalGO.AddComponent<EvaluationScenarioManager>();

        EditorUtility.DisplayDialog(
            "Vampire RL",
            "Custom RL training scaffolding added to the scene.\n\nNext steps:\n1) Ensure a Character exists in the scene.\n2) Press Play, then call RLSystem.Initialize(player) from your bootstrap or via a small MonoBehaviour.\n3) Use RLSystem.StartTraining() when ready.",
            "OK");
    }

    [MenuItem("Vampire RL/Add ML-Agents Components To Selected RLMonster", true)]
    private static bool ValidateAddMLAgentsToSelected()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<RLMonster>() != null;
    }

    [MenuItem("Vampire RL/Add ML-Agents Components To Selected RLMonster")]
    public static void AddMLAgentsToSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Vampire RL", "Select an object with RLMonster component.", "OK");
            return;
        }

        if (go.GetComponent<RLMonster>() == null)
        {
            EditorUtility.DisplayDialog("Vampire RL", "Selected object must have RLMonster.", "OK");
            return;
        }

        // Add ML-Agents runtime components
        var behavior = go.GetComponent<BehaviorParameters>() ?? go.AddComponent<BehaviorParameters>();
        behavior.BehaviorName = "VampireAgent";
        behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
        behavior.TeamId = 0;
        behavior.UseChildSensors = true;
        // Note: In ML-Agents 2.0+, observation space and actions are configured via the model or Agent.CollectObservations
        // ActionSpec is set at runtime by the trained model or in the Agent's Initialize method

        if (go.GetComponent<DecisionRequester>() == null)
        {
            var requester = go.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 1; // every frame
        }

        if (go.GetComponent<MLAgentsRLAgent>() == null)
        {
            go.AddComponent<MLAgentsRLAgent>();
        }

        EditorUtility.DisplayDialog(
            "Vampire RL",
            "ML-Agents components added. Configure BehaviorParameters (actions/obs) as needed.",
            "OK");
    }
}
#endif
