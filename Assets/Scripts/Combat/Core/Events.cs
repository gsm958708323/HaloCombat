namespace Combat.Core
{
    public readonly struct EvEntitySpawn
    {
        public readonly EntityId Id;
        public readonly string BlueprintId;
        public readonly EntityId Owner;
        public readonly string ViewBlueprintId;

        public EvEntitySpawn(EntityId id, string blueprintId, EntityId owner, string viewBlueprintId = null)
        {
            Id = id;
            BlueprintId = blueprintId ?? string.Empty;
            Owner = owner;
            ViewBlueprintId = viewBlueprintId ?? string.Empty;
        }
    }

    public readonly struct EvCue
    {
        public readonly int CueId;
        public readonly EntityId Source;
        public readonly string Name;
        public readonly EntityId Target;
        public readonly SimVec3 Point;
        public readonly bool HasPoint;
        public readonly string AnchorKey;
        public readonly string InstanceKey;
        public readonly bool Loop;
        public readonly bool Stop;

        public EvCue(int cueId, EntityId source, string name)
            : this(cueId, source, name, EntityId.Invalid, SimVec3.Zero, false, string.Empty, string.Empty, false, false)
        {
        }

        public EvCue(int cueId, EntityId source, string name, EntityId target, in SimVec3 point,
            bool hasPoint, string anchorKey, string instanceKey, bool loop, bool stop)
        {
            CueId = cueId;
            Source = source;
            Name = name ?? string.Empty;
            Target = target;
            Point = point;
            HasPoint = hasPoint;
            AnchorKey = anchorKey ?? string.Empty;
            InstanceKey = instanceKey ?? string.Empty;
            Loop = loop;
            Stop = stop;
        }
    }

    public readonly struct EvHurt
    {
        public readonly EntityId Target;
        public EvHurt(EntityId target) => Target = target;
    }

    public readonly struct EvGameplayMessage
    {
        public readonly EntityId Target;
        public readonly string Text;
        public EvGameplayMessage(EntityId target, string text)
        {
            Target = target;
            Text = text ?? string.Empty;
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
        public readonly int SourceFrames;
        public readonly int TargetFrames;
        public int LogicFrames => System.Math.Max(SourceFrames, TargetFrames);
        public int Frames => LogicFrames;
        public bool FreezeSource => SourceFrames > 0;
        public bool FreezeTarget => TargetFrames > 0;
        public EvHitstop(EntityId source, EntityId target, int sourceFrames, int targetFrames)
        {
            Source = source; Target = target;
            SourceFrames = sourceFrames; TargetFrames = targetFrames;
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
