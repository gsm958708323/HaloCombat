namespace Combat.Core
{
    public readonly struct EvCue
    {
        public readonly int CueId;
        public readonly EntityId Source;
        public readonly string Name;
        public EvCue(int cueId, EntityId source, string name)
        {
            CueId = cueId;
            Source = source;
            Name = name ?? string.Empty;
        }
    }

    public readonly struct EvDamage
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly float Amount;
        public readonly bool IsCrit;
        public readonly float ShieldAbsorb;
        public readonly bool IsKill;
        public EvDamage(EntityId source, EntityId target, float amount, bool isCrit, float shieldAbsorb, bool isKill)
        {
            Source = source;
            Target = target;
            Amount = amount;
            IsCrit = isCrit;
            ShieldAbsorb = shieldAbsorb;
            IsKill = isKill;
        }
    }

    public readonly struct EvImmune
    {
        public readonly EntityId Target;
        public readonly EntityId Source;
        public EvImmune(EntityId target, EntityId source)
        {
            Target = target;
            Source = source;
        }
    }

    public readonly struct EvHitstop
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly int LogicFrames;
        public readonly bool FreezeSource;
        public readonly bool FreezeTarget;
        public int Frames => LogicFrames;

        public EvHitstop(EntityId source, EntityId target, int logicFrames)
        {
            Source = source;
            Target = target;
            LogicFrames = logicFrames;
            FreezeSource = true;
            FreezeTarget = true;
        }
    }

    public readonly struct EvHeal
    {
        public readonly EntityId Target;
        public readonly float Amount;
        public EvHeal(EntityId target, float amount)
        {
            Target = target;
            Amount = amount;
        }
    }

    public readonly struct EvEntityDead
    {
        public readonly EntityId Id;
        public readonly EntityId Killer;
        public EvEntityDead(EntityId id, EntityId killer)
        {
            Id = id;
            Killer = killer;
        }
    }

    public readonly struct EvEntityCleanup
    {
        public readonly EntityId Id;
        public readonly string Reason;
        public EvEntityCleanup(EntityId id, string reason)
        {
            Id = id;
            Reason = reason ?? string.Empty;
        }
    }
}
