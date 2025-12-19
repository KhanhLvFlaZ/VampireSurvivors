using System;
using System.Collections.Generic;
using UnityEngine;
using Vampire;

namespace Vampire.RL
{
    /// <summary>
    /// Ensures all desired MonsterTypes are represented at episode start.
    /// - Option A: Register agents per type via RLSystem (no spawn).
    /// - Option B: Spawn one prefab per type off-screen (recommended for real training).
    /// </summary>
    public class MonsterTypePrewarmer : MonoBehaviour
    {
        [Header("Behavior")]
        [SerializeField] private bool registerAllEnumTypes = true;
        [SerializeField] private bool spawnOnePerType = false;
        [SerializeField] private float spawnRadiusFromAnchor = 50f;
        [SerializeField] private Transform spawnAnchor; // if null, uses this.transform
        [SerializeField] private bool destroySpawnedAfterSeconds = true;
        [SerializeField] private float destroyDelaySeconds = 8f;

        [Header("Prefabs per Type (for spawning)")]
        [SerializeField] private List<MonsterTypePrefab> typePrefabs = new List<MonsterTypePrefab>();

        private RLSystem rlSystem;

        [Serializable]
        public class MonsterTypePrefab
        {
            public MonsterType type;
            public GameObject prefab; // Prefab should include RLMonster configured for this type
        }

        private void Awake()
        {
            rlSystem = FindFirstObjectByType<RLSystem>();
            if (rlSystem == null)
            {
                Debug.LogWarning("[MonsterTypePrewarmer] RLSystem not found. Prewarm will only spawn prefabs if configured.");
            }
        }

        private void Start()
        {
            TryPrewarm();
        }

        public void TryPrewarm()
        {
            var anchor = spawnAnchor != null ? spawnAnchor : transform;

            // Build desired type set
            var desiredTypes = new HashSet<MonsterType>();
            if (registerAllEnumTypes)
            {
                foreach (MonsterType t in Enum.GetValues(typeof(MonsterType)))
                    desiredTypes.Add(t);
            }
            foreach (var p in typePrefabs)
                desiredTypes.Add(p.type);

            // Register agents per type (ensures RLSystem coordinates all types)
            if (rlSystem != null)
            {
                foreach (var t in desiredTypes)
                {
                    rlSystem.CreateAgentForMonster(t);
                    Debug.Log($"[MonsterTypePrewarmer] Registered agent for type: {t}");
                }
            }

            // Optionally spawn one per type (for actual experience generation)
            if (spawnOnePerType)
            {
                foreach (var t in desiredTypes)
                {
                    var prefab = GetPrefabForType(t);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[MonsterTypePrewarmer] No prefab mapped for {t}. Skipping spawn.");
                        continue;
                    }

                    var pos = anchor.position + (Vector3)(UnityEngine.Random.insideUnitCircle.normalized * spawnRadiusFromAnchor);
                    var go = Instantiate(prefab, pos, Quaternion.identity);
                    go.name = $"Prewarm_{t}";

                    // Ensure RLMonster exists; if not present, try to add (type must be set on prefab for best results)
                    var rlMonster = go.GetComponent<RLMonster>() ?? go.AddComponent<RLMonster>();

                    // Converter will auto-register; but also nudge RLSystem just in case
                    if (rlSystem != null)
                        rlSystem.CreateAgentForMonster(rlMonster.RLMonsterType);

                    if (destroySpawnedAfterSeconds)
                        Destroy(go, destroyDelaySeconds);

                    Debug.Log($"[MonsterTypePrewarmer] Spawned one {t} at {pos} (cleanup: {destroySpawnedAfterSeconds}).");
                }
            }
        }

        private GameObject GetPrefabForType(MonsterType t)
        {
            for (int i = 0; i < typePrefabs.Count; i++)
            {
                if (typePrefabs[i] != null && typePrefabs[i].prefab != null && typePrefabs[i].type.Equals(t))
                    return typePrefabs[i].prefab;
            }
            return null;
        }
    }
}
