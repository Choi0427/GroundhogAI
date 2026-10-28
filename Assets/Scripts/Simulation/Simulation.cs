using System;
using System.Collections.Generic;

namespace GroundhogAI
{
    // All mechanics run here without Unity objects, rendering, wall clock or policy state.
    public sealed class Simulation
    {
        private sealed class Enemy
        {
            public int Id, Hp = Rules.EnemyHp, BornMs;
            public int X, Y, Vx, Vy;
            public bool Homing;
            public Vec Destination;
            public Vec Position => new Vec(X / 65536f, Y / 65536f);
        }
        private sealed class Bullet
        {
            public int Id;
            public Vec Position, Previous, Velocity;
        }
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Bullet> bullets = new List<Bullet>();
        private OriginalSpawner spawner;
        private int px, py, vx, vy, elapsedMs, shotMs, nextEnemyId, nextBulletId;
        public bool Alive { get; private set; }
        public bool Done { get; private set; }
        public float Time => elapsedMs / 1000f;
        public int Kills { get; private set; }
        public string EndReason { get; private set; }

        public void Reset(int seed)
        {
            var random = new OriginalRandom(seed);
            // Original reset consumes Y, X, then two destination draws (0x4100b0).
            py = Q(random.Range(0, Rules.Height)); px = Q(random.Range(0, Rules.Width));
            random.NextFloat(); random.NextFloat();
            vx = vy = elapsedMs = shotMs = nextEnemyId = nextBulletId = Kills = 0;
            enemies.Clear(); bullets.Clear();
            spawner = new OriginalSpawner(random); spawner.Reset();
            Alive = true; Done = false; EndReason = "running";
        }

        public void Step(AgentAction action)
        {
            if (spawner == null) throw new InvalidOperationException("Call Reset(seed) before Step(action).");
            if (Done) return;
            elapsedMs += Rules.StepMilliseconds;
            UpdatePlayer(action);
            spawner.Tick(elapsedMs, spawn => enemies.Add(new Enemy { Id = nextEnemyId++, BornMs = elapsedMs, X = Q(spawn.Position.X), Y = Q(spawn.Position.Y), Destination = spawn.Destination, Homing = spawn.Homing }));
            var player = new Vec(px / 65536f, py / 65536f);
            foreach (var enemy in enemies) if (elapsedMs > enemy.BornMs) UpdateEnemy(enemy, player);
            UpdateBullets();
            float contact = Rules.PlayerContactRadius + Rules.EnemyRadius;
            foreach (var enemy in enemies)
                if ((enemy.Position - player).LengthSquared < contact * contact)
                {
                    Alive = false; Done = true; EndReason = "enemy_contact"; break;
                }
        }

        private void UpdatePlayer(AgentAction action)
        {
            int limit = Q(action.Fire ? 5f : 7.5f);
            vx = MoveAxis(vx, action.Move.X, limit);
            vy = MoveAxis(vy, action.Move.Y, limit);
            px = Clamp(px + vx, Q(20), Q(1260)); py = Clamp(py + vy, Q(20), Q(700));
            if (!action.Fire) return;
            shotMs += Rules.StepMilliseconds;
            if (shotMs <= 100) return;
            shotMs = 0;
            Vec aim = action.Aim.Normalized;
            if (aim.LengthSquared < 0.5f) return;
            Vec side = new Vec(-aim.Y, aim.X);
            Vec origin = new Vec(px / 65536f, py / 65536f) + aim * 36 + side * (aim.X < 0 ? -7 : 7);
            bullets.Add(new Bullet { Id = nextBulletId++, Position = origin, Previous = origin, Velocity = aim * Rules.BulletSpeedPerSecond });
        }

        private static int MoveAxis(int velocity, float input, int limit)
        {
            if (float.IsNaN(input) || float.IsInfinity(input)) input = 0;
            if (Math.Abs(input) < 0.01f)
            {
                velocity = (velocity >> 8) * 192;
                if (Math.Abs(velocity) < 655) velocity = 0;
            }
            else
            {
                if ((velocity < 0 && input > 0) || (velocity > 0 && input < 0)) velocity = (velocity >> 8) * 192;
                velocity += input > 0 ? 65536 : -65536;
            }
            return Clamp(velocity, -limit, limit);
        }

        private static void UpdateEnemy(Enemy enemy, Vec player)
        {
            Vec desired = ((enemy.Homing ? player : enemy.Destination) - enemy.Position).Normalized;
            if (!enemy.Homing)
            {
                enemy.Vx = Q(desired.X * 2.5f); enemy.Vy = Q(desired.Y * 2.5f);
                enemy.X += enemy.Vx; enemy.Y += enemy.Vy;
                return;
            }
            float current = Q((float)Math.Atan2(enemy.Vy, enemy.Vx)) / 65536f;
            float target = Q((float)Math.Atan2(desired.Y, desired.X)) / 65536f;
            float delta = target - current;
            float turn = delta < 0 ? -Rules.EnemyTurnPerTick : Rules.EnemyTurnPerTick;
            if (Math.Abs(delta) < Rules.EnemyTurnPerTick) turn = delta;
            if (Math.Abs(delta) > 3.140000104904175f) turn = -turn;
            float next = Q(current + turn) / 65536f;
            enemy.Vx = (int)(((long)Q(2.5f) * Q((float)Math.Cos(next))) >> 16);
            enemy.Vy = (int)(((long)Q(2.5f) * Q((float)Math.Sin(next))) >> 16);
            enemy.X += enemy.Vx; enemy.Y += enemy.Vy;
        }

        private void UpdateBullets()
        {
            float radius = Rules.EnemyRadius + Rules.BulletRadius;
            for (int b = bullets.Count - 1; b >= 0; --b)
            {
                Bullet bullet = bullets[b];
                bullet.Previous = bullet.Position;
                bullet.Position += bullet.Velocity * Rules.StepSeconds;
                bool consumed = false;
                // The original checks two endpoints, not a swept capsule (0x413b10).
                for (int e = 0; e < enemies.Count; ++e)
                {
                    Enemy enemy = enemies[e];
                    if ((enemy.Position - bullet.Position).LengthSquared >= radius * radius &&
                        (enemy.Position - bullet.Previous).LengthSquared >= radius * radius) continue;
                    enemy.Hp -= Rules.BulletDamage;
                    if (enemy.Hp <= 0) { enemies.RemoveAt(e); Kills++; }
                    consumed = true; break;
                }
                if (consumed || bullet.Position.X < -5 || bullet.Position.X > 1285 || bullet.Position.Y < -5 || bullet.Position.Y > 725)
                    bullets.RemoveAt(b);
            }
        }

        public Observation GetObservation()
        {
            var result = new Observation
            {
                Time = Time, Alive = Alive, Done = Done, Kills = Kills,
                PlayerPosition = new Vec(px / 65536f, py / 65536f),
                PlayerVelocity = new Vec(vx / 65536f, vy / 65536f) / Rules.StepSeconds,
                Enemies = new EnemyObservation[enemies.Count], Bullets = new BulletObservation[bullets.Count]
            };
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                result.Enemies[i] = new EnemyObservation { Id = e.Id, Hp = e.Hp, Position = e.Position, Velocity = new Vec(e.Vx / 65536f, e.Vy / 65536f) / Rules.StepSeconds };
            }
            for (int i = 0; i < bullets.Count; i++)
                result.Bullets[i] = new BulletObservation { Id = bullets[i].Id, Position = bullets[i].Position, Velocity = bullets[i].Velocity };
            return result;
        }
        private static int Q(float value) => (int)(value * 65536f);
        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
