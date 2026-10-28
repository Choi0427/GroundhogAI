using System;
using System.Collections.Generic;

namespace GroundhogAI
{
    // This is an initial, tunable heuristic. No measured optimality is assumed.
    public sealed class HeuristicPolicy : IAgentPolicy
    {
        public AgentConfig Configuration { get; }
        private readonly Vec[] directions;
        private AgentAction previous;
        private float nextDecision;
        private float lastTime = -1f;
        private int targetId = -1;

        public HeuristicPolicy(AgentConfig configuration)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Configuration.Validate();
            var candidates = new List<Vec> { new Vec(0, 0) };
            for (int i = 0; i < configuration.movementCandidates; i++)
            {
                double angle = i * Math.PI * 2 / configuration.movementCandidates;
                // Original movement uses each axis's input sign: there are eight distinct moving actions.
                float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                var direction = new Vec(Math.Abs(x) < 0.0001f ? 0 : Math.Sign(x), Math.Abs(y) < 0.0001f ? 0 : Math.Sign(y));
                bool duplicate = false;
                foreach (Vec candidate in candidates) if ((candidate - direction).LengthSquared < 0.01f) { duplicate = true; break; }
                if (!duplicate) candidates.Add(direction);
            }
            directions = candidates.ToArray();
            Reset();
        }

        public void Reset()
        {
            previous = new AgentAction(new Vec(0, 0), new Vec(1, 0), false);
            nextDecision = 0;
            lastTime = -1;
            targetId = -1;
        }

        public AgentAction Decide(Observation observation)
        {
            if (observation.Time < lastTime) Reset();
            lastTime = observation.Time;
            if (!observation.Alive || observation.Done) return new AgentAction(new Vec(0, 0), previous.Aim, false);
            if (observation.Time + 0.000001f < nextDecision) return previous;
            nextDecision = observation.Time + Configuration.decisionIntervalSeconds;

            Vec movement = directions[0];
            double best = double.NegativeInfinity;
            for (int i = 0; i < directions.Length; i++)
            {
                double score = ScoreMovement(observation, directions[i]);
                if (score > best)
                {
                    best = score;
                    movement = directions[i];
                }
            }

            int selected = SelectTarget(observation);
            Vec aim = previous.Aim;
            if (selected >= 0)
            {
                EnemyObservation enemy = observation.Enemies[selected];
                targetId = enemy.Id;
                Vec relative = enemy.Position - observation.PlayerPosition;
                float lead = InterceptTime(relative, enemy.Velocity, Rules.BulletSpeedPerSecond);
                aim = (relative + enemy.Velocity * lead).Normalized;
            }
            else targetId = -1;
            previous = new AgentAction(movement, aim, selected >= 0);
            return previous;
        }

        private double ScoreMovement(Observation observation, Vec direction)
        {
            AgentConfig c = Configuration;
            float speed = observation.Enemies.Length > 0 ? Rules.PlayerFiringSpeedPerSecond : Rules.PlayerSpeedPerSecond;
            Vec velocity = direction * speed;
            double risk = 0;
            double wallCost = 0;
            float contactRadius = Rules.PlayerContactRadius + Rules.EnemyRadius;
            Vec end = observation.PlayerPosition;
            for (int sample = 1; sample <= c.predictionSamples; sample++)
            {
                float time = c.predictionSeconds * sample / c.predictionSamples;
                Vec raw = PredictPosition(observation, velocity, time);
                end = ClampPosition(raw);
                float wallDistance = Math.Min(Math.Min(end.X - Rules.PlayerBoundaryMargin, Rules.Width - Rules.PlayerBoundaryMargin - end.X),
                    Math.Min(end.Y - Rules.PlayerBoundaryMargin, Rules.Height - Rules.PlayerBoundaryMargin - end.Y));
                wallCost += Math.Max(0, 1f - wallDistance / c.wallMargin);
                wallCost += (raw - end).Length / (Rules.PlayerSpeedPerSecond * c.predictionSeconds);
                foreach (EnemyObservation enemy in observation.Enemies)
                {
                    // Current measured velocity preserves the original enemy's turning delay.
                    Vec predicted = enemy.Position + enemy.Velocity * time;
                    float clearance = (predicted - end).Length - contactRadius;
                    double proximity = 1.0 / (1.0 + Math.Max(0, clearance) / Math.Max(1f, c.safetyMargin));
                    risk += proximity * proximity / (1.0 + time);
                    if (clearance <= 0) risk += 100 * (1.0 + c.predictionSeconds - time);
                }
            }
            // Also examine continuous closest approach so an enemy cannot pass between samples unnoticed.
            foreach (EnemyObservation enemy in observation.Enemies)
            {
                Vec relativePosition = enemy.Position - observation.PlayerPosition;
                Vec relativeVelocity = enemy.Velocity - (end - observation.PlayerPosition) / c.predictionSeconds;
                float speedSquared = relativeVelocity.LengthSquared;
                float time = speedSquared > 0.001f ? -Dot(relativePosition, relativeVelocity) / speedSquared : 0f;
                time = Math.Max(0f, Math.Min(c.predictionSeconds, time));
                float separation = (relativePosition + relativeVelocity * time).Length;
                if (separation < contactRadius + c.safetyMargin)
                    risk += 20.0 * (contactRadius + c.safetyMargin - separation) / (contactRadius + c.safetyMargin);
            }

            double escapeSpace = EscapeSpace(observation, end, c.predictionSeconds);
            double continuity = Dot(direction, previous.Move);
            return -c.riskWeight * risk / c.predictionSamples
                   -c.wallWeight * wallCost / c.predictionSamples
                   +c.escapeSpaceWeight * escapeSpace
                   +c.movementContinuityWeight * continuity;
        }

        private double EscapeSpace(Observation observation, Vec origin, float predictionTime)
        {
            double free = 0;
            float range = Rules.PlayerContactRadius + Rules.EnemyRadius + Configuration.safetyMargin;
            // Eight exit rays measure room after reaching the candidate endpoint.
            for (int ray = 0; ray < 8; ray++)
            {
                double angle = ray * Math.PI / 4;
                Vec direction = new Vec((float)Math.Cos(angle), (float)Math.Sin(angle));
                Vec candidate = origin + direction * (Rules.PlayerSpeedPerSecond * 0.35f);
                if ((candidate - ClampPosition(candidate)).LengthSquared > 0.01f) continue;
                float clearance = 1f;
                foreach (EnemyObservation enemy in observation.Enemies)
                {
                    Vec offset = enemy.Position + enemy.Velocity * predictionTime - origin;
                    Vec rayVector = candidate - origin;
                    float projected = Math.Max(0f, Math.Min(1f, Dot(offset, rayVector) / Math.Max(1f, rayVector.LengthSquared)));
                    float distance = (offset - rayVector * projected).Length;
                    clearance = Math.Min(clearance, Math.Max(0f, distance - Rules.PlayerContactRadius - Rules.EnemyRadius) / range);
                }
                free += clearance;
            }
            return free / 8.0;
        }

        private int SelectTarget(Observation observation)
        {
            int selected = -1;
            double best = double.NegativeInfinity;
            for (int i = 0; i < observation.Enemies.Length; i++)
            {
                EnemyObservation enemy = observation.Enemies[i];
                if (enemy.Hp <= 0) continue;
                Vec offset = enemy.Position - observation.PlayerPosition;
                float distance = Math.Max(1f, offset.Length);
                float closingSpeed = Math.Max(0f, -Dot(offset / distance, enemy.Velocity - observation.PlayerVelocity));
                float clearance = Math.Max(1f, distance - Rules.PlayerContactRadius - Rules.EnemyRadius);
                float threat = (Rules.EnemySpeedPerSecond + closingSpeed) / clearance;
                double score = Configuration.targetThreatWeight * threat
                    +Configuration.targetWoundedWeight * (Rules.EnemyHp - enemy.Hp) / (double)Rules.EnemyHp
                    +Configuration.targetDistanceWeight * 200.0 / distance
                    +(enemy.Id == targetId ? Configuration.targetPersistenceWeight : 0f);
                if (score > best) { best = score; selected = i; }
            }
            return selected;
        }

        private static float InterceptTime(Vec relative, Vec targetVelocity, float bulletSpeed)
        {
            double a = targetVelocity.LengthSquared - bulletSpeed * bulletSpeed;
            double b = 2 * Dot(relative, targetVelocity);
            double cc = relative.LengthSquared;
            double time = 0;
            if (Math.Abs(a) < 0.0001) { if (b < -0.0001) time = -cc / b; }
            else
            {
                double discriminant = b * b - 4 * a * cc;
                if (discriminant >= 0)
                {
                    double root = Math.Sqrt(discriminant);
                    double t1 = (-b - root) / (2 * a);
                    double t2 = (-b + root) / (2 * a);
                    time = t1 > 0 && t2 > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
                }
            }
            return (float)Math.Max(0, Math.Min(3, time));
        }

        private static float Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y;
        private static Vec PredictPosition(Observation observation, Vec targetVelocity, float time)
        {
            // Short inertial transition is a policy approximation, not a simulation-rule change.
            const float responseSeconds = 0.08f;
            float transition = responseSeconds * (1f - (float)Math.Exp(-time / responseSeconds));
            return observation.PlayerPosition + targetVelocity * time + (observation.PlayerVelocity - targetVelocity) * transition;
        }
        private static Vec ClampPosition(Vec value) => new Vec(
            Math.Max(Rules.PlayerBoundaryMargin, Math.Min(Rules.Width - Rules.PlayerBoundaryMargin, value.X)),
            Math.Max(Rules.PlayerBoundaryMargin, Math.Min(Rules.Height - Rules.PlayerBoundaryMargin, value.Y)));
    }
}
