using System;

namespace GroundhogAI
{
    [Serializable]
    public struct Vec
    {
        public float X, Y;
        public Vec(float x, float y) { X = x; Y = y; }
        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSquared);
        public Vec Normalized => Length > 0.000001f ? this / Length : new Vec();
        public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y);
        public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y);
        public static Vec operator -(Vec a) => new Vec(-a.X, -a.Y);
        public static Vec operator *(Vec a, float b) => new Vec(a.X * b, a.Y * b);
        public static Vec operator *(float b, Vec a) => a * b;
        public static Vec operator /(Vec a, float b) => new Vec(a.X / b, a.Y / b);
        public static float Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y;
    }

    public struct AgentAction
    {
        public Vec Move, Aim;
        public bool Fire;
        public AgentAction(Vec move, Vec aim, bool fire) { Move = move; Aim = aim; Fire = fire; }
    }
    public struct EnemyObservation
    {
        public int Id, Hp;
        public Vec Position, Velocity;
    }
    public struct BulletObservation
    {
        public int Id;
        public Vec Position, Velocity;
    }
    public sealed class Observation
    {
        public float Time;
        public Vec PlayerPosition, PlayerVelocity;
        public EnemyObservation[] Enemies;
        public BulletObservation[] Bullets;
        public bool Alive, Done;
        public int Kills;
    }

    // Original world constants are deliberately separate from configurable AI weights.
    public static class Rules
    {
        public const string Version = "r05-endless-65s-linear-v2";
        public const int Width = 1280, Height = 720, StepMilliseconds = 16;
        public const float StepSeconds = 0.016f;
        public const int EnemyHp = 3, BulletDamage = 1;
        public const float PlayerRadius = 15, PlayerContactRadius = 7.5f, EnemyRadius = 22, BulletRadius = 5;
        public const float PlayerBoundaryMargin = 20;
        public const float PlayerSpeedPerSecond = 468.75f, PlayerFiringSpeedPerSecond = 312.5f;
        public const float EnemySpeedPerSecond = 156.25f, EnemyTurnPerTick = 0.035f;
        public const float BulletSpeedPerSecond = 950;
    }

    // Park–Miller generator recovered at 0x41bd60; not System.Random/Unity.Random.
    public sealed class OriginalRandom
    {
        private long state;
        public OriginalRandom(int seed) { state = seed % 2147483647L; if (state <= 0) state += 2147483646L; }
        public float NextFloat() { state = (state * 16807L) % 2147483647L; return (float)(state * 4.656612875e-10); }
        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
