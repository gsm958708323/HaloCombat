using Combat.Core;

namespace Combat.TrainingCamp
{
    public static class TrainingCampProbe
    {
        public static bool Check(CombatWorld world, EntityId player, EntityId dummy)
        {
            if (world == null || !world.TryGetActor(player, out var p) || !world.TryGetActor(dummy, out var d)) return false;
            if (!p.TryGetComp<InputBufferComp>(out _) || !p.TryGetComp<SkillDirectorComp>(out _)) return false;
            if (!d.TryGetComp<HealthComp>(out _) || !d.TryGetComp<BehaviorTreeComp>(out _)) return false;
            var attr = d.GetComp<AttributeSet>();
            return attr.GetBase(AttrId.MaxHp) >= 100000000f && d.TryGetComp<BuffComp>(out _);
        }
        public static int Count<T>(CombatWorld world) where T : Comp
        {
            int count = 0; if (world == null) return count;
            foreach (var actor in world.RegistryActive()) if (actor.TryGetComp<T>(out _)) count++;
            return count;
        }
    }
}
