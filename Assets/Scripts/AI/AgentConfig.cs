using System;
using System.IO;
using UnityEngine;

namespace GroundhogAI
{
    [Serializable]
    public sealed class AgentConfig
    {
        public string version = "heuristic-v1";
        public float riskWeight = 12f;
        public float wallWeight = 2f;
        public float escapeSpaceWeight = 3f;
        public float movementContinuityWeight = 0.2f;
        public float predictionSeconds = 1.0f;
        public float safetyMargin = 30f;
        public float wallMargin = 80f;
        public float targetThreatWeight = 3f;
        public float targetWoundedWeight = 1.5f;
        public float targetDistanceWeight = 1f;
        public float targetPersistenceWeight = 0.3f;
        public float decisionIntervalSeconds = 0.048f;
        public int movementCandidates = 8;
        public int predictionSamples = 6;

        public static AgentConfig Load(string path = null)
        {
            var config = new AgentConfig();
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!File.Exists(path)) throw new FileNotFoundException("AI configuration file does not exist.", path);
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), config);
            }
            config.Validate();
            return config;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("AI config version must not be empty.");
            Check(riskWeight, "riskWeight", 0f, 10000f);
            Check(wallWeight, "wallWeight", 0f, 10000f);
            Check(escapeSpaceWeight, "escapeSpaceWeight", 0f, 10000f);
            Check(movementContinuityWeight, "movementContinuityWeight", 0f, 10000f);
            Check(predictionSeconds, "predictionSeconds", 0.016f, 10f);
            Check(safetyMargin, "safetyMargin", 0f, 1000f);
            Check(wallMargin, "wallMargin", 1f, 360f);
            Check(targetThreatWeight, "targetThreatWeight", 0f, 10000f);
            Check(targetWoundedWeight, "targetWoundedWeight", 0f, 10000f);
            Check(targetDistanceWeight, "targetDistanceWeight", 0f, 10000f);
            Check(targetPersistenceWeight, "targetPersistenceWeight", 0f, 10000f);
            Check(decisionIntervalSeconds, "decisionIntervalSeconds", 0.016f, 2f);
            if (movementCandidates < 4 || movementCandidates > 64) throw new ArgumentException("movementCandidates must be 4..64.");
            if (predictionSamples < 2 || predictionSamples > 32) throw new ArgumentException("predictionSamples must be 2..32.");
        }

        private static void Check(float value, string name, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException(name + " must be finite and between " + minimum + " and " + maximum + ".");
        }
    }
}
