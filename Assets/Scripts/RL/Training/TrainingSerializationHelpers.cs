using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire.RL.Training
{
    /// <summary>
    /// Serializable dictionary wrapper for episode monster metrics.
    /// Workaround for Unity's inability to serialize dictionaries.
    /// </summary>
    [Serializable]
    public class SerializableMonsterMetricsDict
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public EpisodeMonsterMetrics value;
        }

        public List<Entry> entries = new List<Entry>();

        public void Add(string key, EpisodeMonsterMetrics value)
        {
            entries.Add(new Entry { key = key, value = value });
        }

        public Dictionary<string, EpisodeMonsterMetrics> ToDictionary()
        {
            var dict = new Dictionary<string, EpisodeMonsterMetrics>();
            foreach (var entry in entries)
            {
                dict[entry.key] = entry.value;
            }
            return dict;
        }

        public static SerializableMonsterMetricsDict FromDictionary(Dictionary<string, EpisodeMonsterMetrics> dict)
        {
            var wrapper = new SerializableMonsterMetricsDict();
            foreach (var kvp in dict)
            {
                wrapper.Add(kvp.Key, kvp.Value);
            }
            return wrapper;
        }
    }

    /// <summary>
    /// Alternative: export to JSON-friendly format
    /// </summary>
    [Serializable]
    public class EpisodeMetricsExport
    {
        public int episode;
        public string timestamp;
        public float reward;
        public float length;
        public float averageRewardPerStep;
        public List<MonsterMetricsEntry> monsterMetrics;

        public EpisodeMetricsExport(EpisodeMetrics metrics)
        {
            episode = metrics.episode;
            timestamp = metrics.timestamp.ToString("O");
            reward = metrics.reward;
            length = metrics.length;
            averageRewardPerStep = metrics.averageRewardPerStep;
            monsterMetrics = new List<MonsterMetricsEntry>();

            if (metrics.monsterMetrics != null)
            {
                foreach (var kvp in metrics.monsterMetrics)
                {
                    monsterMetrics.Add(new MonsterMetricsEntry
                    {
                        monsterType = kvp.Value.monsterType,
                        episodeCount = kvp.Value.episodeCount,
                        averageReward = kvp.Value.averageReward,
                        bestReward = kvp.Value.bestReward,
                        recentAverageReward = kvp.Value.recentAverageReward,
                        explorationRate = kvp.Value.explorationRate,
                        survivalRate = kvp.Value.survivalRate,
                        totalSteps = kvp.Value.totalSteps,
                        lossValue = kvp.Value.lossValue
                    });
                }
            }
        }
    }

    [Serializable]
    public class MonsterMetricsEntry
    {
        public string monsterType;
        public int episodeCount;
        public float averageReward;
        public float bestReward;
        public float recentAverageReward;
        public float explorationRate;
        public float survivalRate;
        public int totalSteps;
        public float lossValue;
    }
}
