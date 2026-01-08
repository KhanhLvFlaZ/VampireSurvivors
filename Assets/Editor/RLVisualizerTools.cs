#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vampire.RL.Visualization;

public static class RLVisualizerTools
{
    [MenuItem("Tools/RL/Select First RL Monster Visualizer")]
    public static void SelectFirstRLVisualizer()
    {
        var viz = Object.FindFirstObjectByType<RLMonsterVisualizer>();
        if (viz == null)
        {
            EditorUtility.DisplayDialog("RL Visualizer", "No RLMonsterVisualizer found in the open scene.", "OK");
            return;
        }
        Selection.activeGameObject = viz.gameObject;
        EditorGUIUtility.PingObject(viz.gameObject);
    }

    [MenuItem("Tools/RL/Visualizer/Boost Sorting Order (5000)")]
    public static void BoostSortingOrder()
    {
        int count = 0;
        var all = Object.FindObjectsByType<RLMonsterVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var viz in all)
        {
            var t = viz.transform.Find("RLVisualizerUI");
            if (t == null) continue;
            var canvas = t.GetComponent<Canvas>();
            if (canvas == null) continue;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
            count++;
        }
        EditorUtility.DisplayDialog("RL Visualizer", $"Updated sorting order to 5000 for {count} visualizer(s).", "OK");
    }

    [MenuItem("Tools/RL/Visualizer/Reset Sorting Order (1000)")]
    public static void ResetSortingOrder()
    {
        int count = 0;
        var all = Object.FindObjectsByType<RLMonsterVisualizer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var viz in all)
        {
            var t = viz.transform.Find("RLVisualizerUI");
            if (t == null) continue;
            var canvas = t.GetComponent<Canvas>();
            if (canvas == null) continue;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            count++;
        }
        EditorUtility.DisplayDialog("RL Visualizer", $"Reset sorting order to 1000 for {count} visualizer(s).", "OK");
    }
}
#endif
