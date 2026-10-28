using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GroundhogAI
{
    // Owns the clock and lifecycle. Simulation and policy never depend on this component.
    public sealed class GameHost : MonoBehaviour
    {
        public Simulation World { get; private set; }
        public Observation Current { get; private set; }
        public AgentAction LastAction { get; private set; }
        public int Seed { get; private set; } = 1;
        public int Episode { get; private set; }
        public bool AiEnabled = true;
        public bool Paused;
        public string LastResult = "No completed episode";
        public string ConfigVersion { get; private set; }
        public string Error { get; private set; }
        private HeuristicPolicy policy;
        private string configPath;
        private float accumulator, restartAt;
        private bool evaluation;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            evaluation = Array.IndexOf(args, "--evaluate") >= 0;
            if (evaluation) { Application.Quit(EvaluationRunner.Run(args)); return; }
            Application.targetFrameRate = 60;
            configPath = Path.Combine(Application.streamingAssetsPath, "ai-default.json");
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--ai-config") configPath = Path.GetFullPath(args[++i]);
                else if (args[i] == "--seed") Seed = int.Parse(args[++i]);
            }
            World = new Simulation();
            BeginEpisode(false);
        }

        public void BeginEpisode(bool nextSeed)
        {
            if (evaluation || World == null) return;
            try
            {
                var config = AgentConfig.Load(configPath);
                policy = new HeuristicPolicy(config); policy.Reset(); ConfigVersion = config.version;
                if (nextSeed) Seed = Seed == int.MaxValue ? 1 : Seed + 1;
                World.Reset(Seed); Current = World.GetObservation(); Episode++;
                accumulator = 0; restartAt = 0; Error = null;
            }
            catch (Exception exception) { Error = exception.Message; Paused = true; Debug.LogError(exception); }
        }

        private void Update()
        {
            if (evaluation || World == null || Error != null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) AiEnabled = !AiEnabled;
                if (keyboard.spaceKey.wasPressedThisFrame) Paused = !Paused;
                if (keyboard.rKey.wasPressedThisFrame) BeginEpisode(false);
            }
            if (Paused) return;
            if (World.Done)
            {
                if (restartAt == 0)
                {
                    LastResult = $"Seed {Seed}  /  {World.Time:F3}s  /  {World.Kills} kills  /  {World.EndReason}";
                    restartAt = UnityEngine.Time.unscaledTime + 1.5f;
                }
                if (UnityEngine.Time.unscaledTime >= restartAt) BeginEpisode(true);
                return;
            }
            accumulator += Mathf.Min(UnityEngine.Time.unscaledDeltaTime, 0.25f);
            while (accumulator >= Rules.StepSeconds && !World.Done)
            {
                Current = World.GetObservation();
                LastAction = AiEnabled ? policy.Decide(Current) : ManualAction();
                World.Step(LastAction); accumulator -= Rules.StepSeconds;
            }
            Current = World.GetObservation();
        }

        private AgentAction ManualAction()
        {
            var k = Keyboard.current; var m = Mouse.current;
            float x = k == null ? 0 : ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
            float y = k == null ? 0 : ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0) - ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0);
            Rect arena = GameRenderer.ArenaRect;
            Vector2 mouse = m == null ? Vector2.zero : m.position.ReadValue();
            Vec target = new Vec((mouse.x - arena.x) * Rules.Width / arena.width,
                (Screen.height - mouse.y - arena.y) * Rules.Height / arena.height);
            return new AgentAction(new Vec(x, y), (target - Current.PlayerPosition).Normalized, m != null && m.leftButton.isPressed);
        }
    }
}
