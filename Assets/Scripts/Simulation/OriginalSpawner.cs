using System;

namespace GroundhogAI
{
    public struct OriginalSpawn
    {
        public Vec Position;
        public Vec Destination;
        public bool Homing;

        public OriginalSpawn(Vec position, Vec destination, bool homing)
        {
            Position = position;
            Destination = destination;
            Homing = homing;
        }
    }

    /// <summary>
    /// Release 5's initial level: data/levels/level_cluster_small.txt.
    /// The executable advances level index 0 to 1 before starting gameplay.
    /// This is a finite authored schedule, not a decreasing-interval formula.
    /// See Analysis/original-spawning.md for static-analysis evidence.
    /// </summary>
    public sealed class OriginalSpawner
    {
        private readonly OriginalRandom random;
        private int nextEvent;
        private int lastSpawnTime;
        private int cycle;
        public int Difficulty => cycle + 1;

        public int DurationMs => 65000;
        public bool Complete { get; private set; }
        public int EventsDispatched => nextEvent;
        public const int SourceEventCount = 53;
        public const int ExpandedEnemyCount = 89;

        private readonly struct Event
        {
            public readonly int TimeMs;
            public readonly int SourceType;
            public Event(int timeMs, int sourceType)
            {
                TimeMs = timeMs;
                SourceType = sourceType;
            }
        }

        // Order matters: the duplicate 22100 timestamp fires on successive ticks.
        private static readonly Event[] Schedule =
        {
            new Event(1248,3), new Event(2544,3), new Event(4144,3),
            new Event(5008,3), new Event(5856,3), new Event(6512,3),
            new Event(7168,3), new Event(7296,3), new Event(7872,3),
            new Event(8448,3), new Event(9136,3), new Event(9840,3),
            new Event(9936,3), new Event(9984,3), new Event(10000,3),
            new Event(15100,4), new Event(16000,4), new Event(16050,1),
            new Event(16100,3), new Event(17500,3), new Event(18500,3),
            new Event(19000,1), new Event(20000,4), new Event(22100,3),
            new Event(22100,4), new Event(22200,3), new Event(23000,1),
            new Event(26100,4), new Event(26200,3), new Event(27200,3),
            new Event(28200,3), new Event(29200,3), new Event(30200,4),
            new Event(32200,5), new Event(33200,4), new Event(34200,5),
            new Event(35200,3), new Event(36200,3), new Event(37200,3),
            new Event(38200,3), new Event(39200,3), new Event(40200,3),
            new Event(41200,3), new Event(42200,3), new Event(43200,3),
            new Event(44200,3), new Event(45200,3), new Event(46200,3),
            new Event(47200,3), new Event(48200,3), new Event(49200,3),
            new Event(50200,3), new Event(51200,3)
        };

        public OriginalSpawner(OriginalRandom random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Reset();
        }

        public void Reset()
        {
            nextEvent = 0;
            lastSpawnTime = 0;
            Complete = false;
            cycle = 0;
        }

        public void Tick(int timeMs, Action<OriginalSpawn> spawn)
        {
            int currentCycle = timeMs / DurationMs;
            if (currentCycle != cycle)
            {
                cycle = currentCycle;
                nextEvent = 0;
            }
            int localTime = timeMs % DurationMs;
            if (timeMs <= lastSpawnTime || nextEvent >= Schedule.Length)
                return;

            Event entry = Schedule[nextEvent];
            // 0x412d20: strict greater-than, and at most one event per tick.
            if (localTime <= entry.TimeMs)
                return;
            nextEvent++;
            lastSpawnTime = timeMs;

            // Requested endless extension: one additional independently placed
            // copy of each authored event per 65-second cycle.
            for (int copy = 0; copy < Difficulty; copy++) SpawnEvent(entry, spawn);
        }

        private void SpawnEvent(Event entry, Action<OriginalSpawn> spawn)
        {

            DrawEdgePair(out Vec origin, out Vec destination);
            // Preserve the original ordinary straight mover (type 3).
            // Only enhanced/group slots are replaced by normal homing enemies.
            spawn(new OriginalSpawn(origin, destination, entry.SourceType != 3));
            if (entry.SourceType != 4 && entry.SourceType != 5)
                return;

            // 0x404dd0: both cluster codes expand to exactly five instances.
            // Retain their initial offsets/count/time, but the caller creates
            // independent normal enemies with no shared movement/formation.
            Vec tangent;
            Vec outward;
            if (origin.X < 0 || origin.X > 1280)
            {
                tangent = new Vec(0, 60);
                outward = new Vec(origin.X < 0 ? -60 : 60, 0);
            }
            else
            {
                tangent = new Vec(60, 0);
                outward = new Vec(0, origin.Y < 0 ? -60 : 60);
            }
            spawn(new OriginalSpawn(new Vec(origin.X - tangent.X + outward.X,
                origin.Y - tangent.Y + outward.Y), destination, true));
            spawn(new OriginalSpawn(new Vec(origin.X + tangent.X + outward.X,
                origin.Y + tangent.Y + outward.Y), destination, true));
            spawn(new OriginalSpawn(new Vec(origin.X + outward.X, origin.Y + outward.Y),
                destination, true));
            spawn(new OriginalSpawn(new Vec(origin.X + 2 * outward.X, origin.Y + 2 * outward.Y),
                destination, true));
        }

        private void DrawEdgePair(out Vec origin, out Vec destination)
        {
            // 0x404ce0 consumes six uniforms and swaps only the fixed edge axis.
            float ay = random.NextFloat() * 720f;
            float ax = random.NextFloat() * 1280f;
            float by = random.NextFloat() * 720f;
            float bx = random.NextFloat() * 1280f;
            float flip = random.NextFloat();
            float axis = random.NextFloat();
            if (axis < 0.5f)
            {
                origin = new Vec(flip < 0.5f ? 1355f : -75f, ay);
                destination = new Vec(flip < 0.5f ? -75f : 1355f, by);
            }
            else
            {
                origin = new Vec(ax, flip < 0.5f ? 795f : -75f);
                destination = new Vec(bx, flip < 0.5f ? -75f : 795f);
            }
        }
    }
}
