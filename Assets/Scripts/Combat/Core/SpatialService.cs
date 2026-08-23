namespace Combat.Core
{
    public sealed class SpatialService : ISpatialQuery
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;

        public SpatialService(CombatWorld world, IntentQueue intents)
        {
            _world = world;
            _intents = intents;
        }

        public void QueryCircle(in SimVec3 center, float radius, in SpatialFilter filter, List<SpatialHit> results)
        {
            results.Clear();
            var actors = _world.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.TryGetComp<TransformComp>(out var tf)) continue;
                if (!a.TryGetComp<HurtboxComp>(out var hurt) || !hurt.CanBeHit) continue;
                if (a.Id == filter.IgnoreEntity) continue;

                int team = 0;
                if (a.TryGetComp<TeamComp>(out var t)) team = t.Team;
                if (team == filter.ExcludeTeam) continue;

                if (!OverlapCircle(center, radius, tf.Position, hurt.Radius)) continue;

                results.Add(new SpatialHit
                {
                    Entity = a.Id,
                    Position = tf.Position,
                    HurtRadius = hurt.Radius,
                    Team = team
                });
            }
        }

        static bool OvelapCircle(in SimVec3 c, float r, in SimVec3 p, float hr)
        {
            return (c.X - p.X) * (c.X - p.X) + (c.Y - p.Y) * (c.Y - p.Y) + (c.Z - p.Z) * (c.Z - p.Z) <= (r + hr) * (r + hr);
        }
    }

    public readonly struct SpatialHit
    {
        public EntityId Entity;
        public SimVec3 Position;
        public float HurtRadius;
        public int Team;
    }

    public readonly struct SpatialFilter
    {
        public int? ExcludeTeam;
        public EntityId IgnoreEntity;
        public bool HitOwner;
    }
}
