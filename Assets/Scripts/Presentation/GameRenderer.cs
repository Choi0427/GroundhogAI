using UnityEngine;

namespace GroundhogAI
{
    // Read-only presentation. Coordinates match the original 1280x720, Y points down.
    public sealed class GameRenderer : MonoBehaviour
    {
        private GameHost host;
        private Texture2D player, enemy, bullet, paper;
        public static Rect ArenaRect
        {
            get
            {
                float scale = Mathf.Min(Screen.width / 1280f, Mathf.Max(1, Screen.height - 120) / 720f);
                return new Rect((Screen.width - 1280 * scale) / 2, 68, 1280 * scale, 720 * scale);
            }
        }
        private void Awake()
        {
            host = GetComponent<GameHost>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--evaluate") >= 0) { enabled = false; return; }
            player = Resources.Load<Texture2D>("Original/player");
            enemy = Resources.Load<Texture2D>("Original/enemy");
            bullet = Resources.Load<Texture2D>("Original/bullet");
            paper = Resources.Load<Texture2D>("Original/paper");
        }
        private void OnGUI()
        {
            if (host == null) return;
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 12, 800, 24), "GROUNDHOG AI   /   RELEASE 5 · DEFAULT LEVEL");
            if (host.Error != null) { GUI.Label(new Rect(20, 48, Screen.width - 40, 100), host.Error); return; }
            Observation state = host.Current;
            if (state == null) return;
            GUI.Label(new Rect(20, 36, 900, 24), $"Episode {host.Episode}  ·  Seed {host.Seed}  ·  {state.Time:F2}s  ·  Difficulty {1 + (int)(state.Time / 65f)}  ·  {state.Kills} kills  ·  {state.Enemies.Length} enemies  ·  {host.ConfigVersion}");
            if (GUI.Button(new Rect(Screen.width - 340, 16, 95, 30), host.AiEnabled ? "AI: ON" : "MANUAL")) host.AiEnabled = !host.AiEnabled;
            if (GUI.Button(new Rect(Screen.width - 235, 16, 95, 30), host.Paused ? "Resume" : "Pause")) host.Paused = !host.Paused;
            if (GUI.Button(new Rect(Screen.width - 130, 16, 110, 30), "Restart seed")) host.BeginEpisode(false);
            Rect arena = ArenaRect;
            GUI.color = new Color(0.88f, 0.87f, 0.80f);
            GUI.DrawTexture(arena, paper != null ? paper : Texture2D.whiteTexture);
            GUI.color = Color.white;
            foreach (EnemyObservation e in state.Enemies)
            {
                DrawActor(enemy, e.Position, 44, e.Velocity);
                Vector2 at = ToScreen(e.Position);
                float scale = arena.width / Rules.Width;
                GUI.color = new Color(0.2f, 0.2f, 0.18f); GUI.DrawTexture(new Rect(at.x - 13 * scale, at.y - 28 * scale, 26 * scale, 4 * scale), Texture2D.whiteTexture);
                GUI.color = new Color(0.8f, 0.2f, 0.1f); GUI.DrawTexture(new Rect(at.x - 13 * scale, at.y - 28 * scale, 26 * scale * e.Hp / 3f, 4 * scale), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            foreach (BulletObservation b in state.Bullets) DrawActor(bullet, b.Position, 10, b.Velocity);
            GUI.color = state.Alive ? Color.white : new Color(0.85f, 0.2f, 0.2f);
            DrawActor(player, state.PlayerPosition, 38, host.LastAction.Aim);
            GUI.color = Color.white;
            if (state.Done) GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height / 2 - 28, 340, 56), host.World.EndReason + "\nNew episode starts automatically");
            GUI.Label(new Rect(20, Screen.height - 46, Screen.width - 40, 22), "TAB: AI / manual    SPACE: pause    R: restart seed    Manual: WASD + mouse + left click");
            GUI.Label(new Rect(20, Screen.height - 24, Screen.width - 40, 22), "Last: " + host.LastResult);
        }
        private static Vector2 ToScreen(Vec position)
        {
            Rect arena = ArenaRect;
            return new Vector2(arena.x + position.X * arena.width / Rules.Width, arena.y + position.Y * arena.height / Rules.Height);
        }
        private static void DrawActor(Texture texture, Vec position, float size, Vec direction)
        {
            Vector2 center = ToScreen(position);
            Rect arena = ArenaRect;
            if (position.X < -30 || position.X > Rules.Width + 30 || position.Y < -30 || position.Y > Rules.Height + 30) return;
            float diameter = size * arena.width / Rules.Width;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.Y, direction.X) * Mathf.Rad2Deg, center);
            GUI.DrawTexture(new Rect(center.x - diameter / 2, center.y - diameter / 2, diameter, diameter), texture != null ? texture : Texture2D.whiteTexture, ScaleMode.ScaleToFit);
            GUI.matrix = saved;
        }
    }
}
