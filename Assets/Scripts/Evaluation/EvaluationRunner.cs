using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GroundhogAI
{
    // Called only when the user launches the player with --evaluate.
    public static class EvaluationRunner
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Serializable]
        public sealed class EpisodeResult
        {
            public int episode;
            public int seed;
            public float survivalSeconds;
            public int kills;
            public string endReason;
            public string configVersion;
            public string configSha256;
            public string rulesVersion;
        }

        [Serializable]
        public sealed class EpisodeReport
        {
            public EpisodeResult[] episodes;
        }

        [Serializable]
        public sealed class Summary
        {
            public string createdUtc;
            public int episodes;
            public int startSeed;
            public float requestedLimitSeconds;
            public float tickSeconds;
            public double meanSurvivalSeconds;
            public double medianSurvivalSeconds;
            public double longestSurvivalSeconds;
            public int timeLimitEpisodes;
            public int stageCompletedEpisodes;
            public string configVersion;
            public string configSha256;
            public string rulesVersion;
            public string outputDirectory;
        }

        public static int Run(string[] args)
        {
            try
            {
                Dictionary<string, string> options = Parse(args);
                int episodes = ReadInt(options, "--episodes", 20);
                int seed = ReadInt(options, "--seed", 1);
                float maxSeconds = ReadFloat(options, "--max-seconds", 300f);
                if (episodes < 1 || episodes > 1000000) throw new ArgumentException("--episodes must be 1..1000000.");
                if (float.IsNaN(maxSeconds) || float.IsInfinity(maxSeconds) || maxSeconds < Rules.StepSeconds || maxSeconds > 86400f)
                    throw new ArgumentException("--max-seconds must be finite and between one simulation tick and 86400.");
                if ((long)seed + episodes - 1 > int.MaxValue) throw new ArgumentException("Seed sequence exceeds Int32.MaxValue.");

                string configPath = options.TryGetValue("--ai-config", out string path) ? Path.GetFullPath(path) : null;
                AgentConfig config = AgentConfig.Load(configPath);
                string configJson = JsonUtility.ToJson(config, true);
                string hash = Hash(configJson);
                string output = Path.GetFullPath(options.TryGetValue("--results", out string resultPath) ? resultPath :
                    Path.Combine(Environment.CurrentDirectory, "Evaluation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", Invariant)));
                string[] reportNames = { "episodes.json", "episodes.csv", "summary.json", "ai-config.json" };
                foreach (string name in reportNames)
                    if (File.Exists(Path.Combine(output, name))) throw new IOException("Existing evaluation report: " + Path.Combine(output, name) + ". Choose a new results directory.");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "ai-config.json"), configJson, new UTF8Encoding(false));

                var results = new EpisodeResult[episodes];
                var sim = new Simulation();
                var policy = new HeuristicPolicy(config);
                var csv = new StringBuilder("episode,seed,survivalSeconds,kills,endReason,configVersion,configSha256,rulesVersion\n");
                var times = new double[episodes];
                double total = 0;
                int limited = 0;
                int completed = 0;
                for (int episode = 0; episode < episodes; episode++)
                {
                    int episodeSeed = seed + episode;
                    sim.Reset(episodeSeed);
                    policy.Reset();
                    while (!sim.Done && sim.Time < maxSeconds)
                        sim.Step(policy.Decide(sim.GetObservation()));

                    string reason = !sim.Done ? "time_limit" : sim.EndReason;
                    if (!sim.Done) limited++;
                    if (reason == "stage_completed") completed++;
                    var result = new EpisodeResult
                    {
                        episode = episode + 1,
                        seed = episodeSeed,
                        survivalSeconds = sim.Time,
                        kills = sim.Kills,
                        endReason = reason,
                        configVersion = config.version,
                        configSha256 = hash,
                        rulesVersion = Rules.Version
                    };
                    results[episode] = result;
                    times[episode] = sim.Time;
                    total += sim.Time;
                    csv.Append(result.episode).Append(',').Append(result.seed).Append(',')
                        .Append(result.survivalSeconds.ToString("R", Invariant)).Append(',').Append(result.kills).Append(',')
                        .Append(Csv(result.endReason)).Append(',').Append(Csv(result.configVersion)).Append(',')
                        .Append(Csv(result.configSha256)).Append(',').Append(Csv(result.rulesVersion)).Append('\n');
                    Debug.Log(string.Format(Invariant, "Episode {0}/{1}: seed={2}, survival={3:F3}s, kills={4}, end={5}",
                        episode + 1, episodes, episodeSeed, sim.Time, sim.Kills, reason));
                }
                Array.Sort(times);
                var summary = new Summary
                {
                    createdUtc = DateTime.UtcNow.ToString("o", Invariant),
                    episodes = episodes,
                    startSeed = seed,
                    requestedLimitSeconds = maxSeconds,
                    tickSeconds = Rules.StepSeconds,
                    meanSurvivalSeconds = total / episodes,
                    medianSurvivalSeconds = episodes % 2 == 1 ? times[episodes / 2] : (times[episodes / 2 - 1] + times[episodes / 2]) / 2,
                    longestSurvivalSeconds = times[episodes - 1],
                    timeLimitEpisodes = limited,
                    stageCompletedEpisodes = completed,
                    configVersion = config.version,
                    configSha256 = hash,
                    rulesVersion = Rules.Version,
                    outputDirectory = output
                };
                File.WriteAllText(Path.Combine(output, "episodes.json"), JsonUtility.ToJson(new EpisodeReport { episodes = results }, true), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(output, "episodes.csv"), csv.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(output, "summary.json"), JsonUtility.ToJson(summary, true), new UTF8Encoding(false));
                Debug.Log(string.Format(Invariant, "Evaluation complete: mean={0:F3}s, median={1:F3}s, longest={2:F3}s. Results: {3}",
                    summary.meanSurvivalSeconds, summary.medianSurvivalSeconds, summary.longestSurvivalSeconds, output));
                return 0;
            }
            catch (Exception exception)
            {
                Debug.LogError("Evaluation failed: " + exception);
                return 2;
            }
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key == "--evaluate" || !key.StartsWith("--", StringComparison.Ordinal)) continue;
                if (key != "--episodes" && key != "--seed" && key != "--max-seconds" && key != "--ai-config" && key != "--results")
                    throw new ArgumentException("Unknown evaluation option: " + key);
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Missing value for " + key);
                if (result.ContainsKey(key)) throw new ArgumentException("Duplicate evaluation option: " + key);
                result.Add(key, args[++i]);
            }
            return result;
        }

        private static int ReadInt(Dictionary<string, string> args, string key, int fallback) =>
            args.TryGetValue(key, out string value) ? int.Parse(value, NumberStyles.Integer, Invariant) : fallback;
        private static float ReadFloat(Dictionary<string, string> args, string key, float fallback) =>
            args.TryGetValue(key, out string value) ? float.Parse(value, NumberStyles.Float, Invariant) : fallback;
        private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}
