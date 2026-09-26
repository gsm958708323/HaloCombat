using System;

namespace Combat.Core
{
    public readonly struct ProjectileMotionContext
    {
        public readonly ProjectileDefinition Definition;
        public readonly SimVec3 Position;
        public readonly SimVec3? OwnerPosition;
        public readonly float Yaw, Age, NextAge, GroundY, Delta;
        public ProjectileMotionContext(ProjectileDefinition definition, SimVec3 position, float yaw,
            float age, float nextAge, float groundY, float delta, SimVec3? ownerPosition)
        {
            Definition = definition; Position = position; Yaw = yaw; Age = age;
            NextAge = nextAge; GroundY = groundY; Delta = delta; OwnerPosition = ownerPosition;
        }
    }

    public readonly struct ProjectileMotionResult
    {
        public readonly SimVec3 Position, Velocity;
        public readonly float Yaw;
        public readonly bool Touchdown;
        public ProjectileMotionResult(SimVec3 position, SimVec3 velocity, float yaw, bool touchdown)
        { Position = position; Velocity = velocity; Yaw = yaw; Touchdown = touchdown; }
    }

    public interface IProjectileMotion
    {
        ProjectileMotionResult Evaluate(in ProjectileMotionContext context);
    }

    /// <summary>Shared immutable strategies. Collision and lifetime remain in ProjectileService.</summary>
    public static class ProjectileMotions
    {
        static readonly IProjectileMotion Linear = new LinearMotion();
        static readonly IProjectileMotion Accelerate = new AcceleratingMotion();
        static readonly IProjectileMotion Return = new ReturningMotion();
        static readonly IProjectileMotion Bounce = new BouncingMotion();
        public static IProjectileMotion Resolve(ProjectileMotionKind kind)
        {
            switch (kind)
            {
                case ProjectileMotionKind.Linear:
                case ProjectileMotionKind.ImmediateHoming: return Linear;
                case ProjectileMotionKind.Accelerate: return Accelerate;
                case ProjectileMotionKind.ReturnToOwner: return Return;
                case ProjectileMotionKind.Bounce: return Bounce;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        static bool Touchdown(in ProjectileMotionContext c)
        {
            var phases = c.Definition.GroundPhaseAt;
            if (phases == null) return false;
            foreach (float t in phases) if (t > c.Age && t <= c.NextAge) return true;
            return false;
        }
        static SimVec3 Forward(in ProjectileMotionContext c, float scale = 1f)
        {
            var f = LocomotionComp.ForwardFromYaw(c.Yaw);
            return new SimVec3(f.X * c.Definition.Speed * scale, 0f, f.Z * c.Definition.Speed * scale);
        }
        static ProjectileMotionResult Integrate(in ProjectileMotionContext c, SimVec3 velocity)
            => new ProjectileMotionResult(c.Position + new SimVec3(velocity.X * c.Delta,
                velocity.Y * c.Delta, velocity.Z * c.Delta), velocity, c.Yaw, Touchdown(c));

        sealed class LinearMotion : IProjectileMotion
        {
            public ProjectileMotionResult Evaluate(in ProjectileMotionContext c) => Integrate(c, Forward(c));
        }
        sealed class AcceleratingMotion : IProjectileMotion
        {
            public ProjectileMotionResult Evaluate(in ProjectileMotionContext c)
            {
                float t = Math.Max(0f, c.Age);
                float pivot = c.Definition.MotionParam > 0f ? c.Definition.MotionParam : 5f;
                return Integrate(c, Forward(c, 2f * t / (t + pivot)));
            }
        }
        sealed class ReturningMotion : IProjectileMotion
        {
            public ProjectileMotionResult Evaluate(in ProjectileMotionContext c)
            {
                float back = c.Definition.MotionParam > 0f ? c.Definition.MotionParam : 1f;
                if (c.Age < back)
                    return Integrate(c, Forward(c, (float)Math.Sin(c.Age / back * Math.PI) + .1f));
                if (c.OwnerPosition.HasValue)
                {
                    var p = c.OwnerPosition.Value;
                    float dx = p.X - c.Position.X, dz = p.Z - c.Position.Z;
                    float len = (float)Math.Sqrt(dx * dx + dz * dz);
                    float scale = (float)Math.Sin(Math.Min((c.Age - back) / back * Math.PI, .5)) + .1f;
                    if (len > .0001f)
                        return Integrate(c, new SimVec3(dx / len * c.Definition.Speed * scale, 0f,
                            dz / len * c.Definition.Speed * scale));
                }
                return Integrate(c, Forward(c));
            }
        }
        sealed class BouncingMotion : IProjectileMotion
        {
            public ProjectileMotionResult Evaluate(in ProjectileMotionContext c)
            {
                var result = Integrate(c, Forward(c));
                var position = result.Position;
                position.Y = c.GroundY + (result.Touchdown ? 0f : Height(c.Definition, c.NextAge));
                var velocity = result.Velocity;
                velocity.Y = (position.Y - c.Position.Y) / Math.Max(c.Delta, .0001f);
                return new ProjectileMotionResult(position, velocity, c.Yaw, result.Touchdown);
            }
            static float Height(ProjectileDefinition def, float age)
            {
                var phases = def.GroundPhaseAt;
                if (def.BounceHeight <= 0f || phases == null || phases.Length == 0 || age <= 0f || phases[0] <= 0f)
                    return 0f;
                float start = 0f;
                foreach (float end in phases)
                {
                    float duration = end - start;
                    if (duration > 0f && age <= end)
                    {
                        float t = Math.Max(0f, Math.Min(1f, (age - start) / duration));
                        float scale = duration / phases[0];
                        return 4f * def.BounceHeight * scale * scale * t * (1f - t);
                    }
                    start = end;
                }
                return 0f;
            }
        }
    }
}
