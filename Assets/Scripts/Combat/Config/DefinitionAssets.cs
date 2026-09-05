using System;
using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/HitProfile")]
    public sealed partial class HitProfileAsset : ScriptableObject
    {
        public DamageEffectAsset Damage;
        public HitStunAsset Stun;
        public KnockbackAsset Knockback;
        public IFrameAsset IFrame;
        IEffect[] _baked;

        public IEffect[] Bake()
        {
            if (_baked != null) return _baked;
            var list = new List<IEffect>(4);
            if (Damage) list.Add(Damage.Bake());
            if (Stun) list.Add(Stun.Bake());
            if (Knockback) list.Add(Knockback.Bake());
            if (IFrame) list.Add(IFrame.Bake());
            _baked = list.ToArray();
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            if (Damage) Damage.ClearCache();
            if (Stun) Stun.ClearCache();
            if (Knockback) Knockback.ClearCache();
            if (IFrame) IFrame.ClearCache();
        }

        void OnValidate() => ClearCache();
    }

    [CreateAssetMenu(menuName = "Combat/DurationSpec")]
    public sealed partial class DurationSpecAsset : ScriptableObject
    {
        public int BuffId;
        public float Duration = 3f;
        public float TickInterval;
        public int MaxStacks = 1;
        public StackPolicy Stack = StackPolicy.AddStack;
        public int MutexGroup;
        public AttrId ModAttr = AttrId.Atk;
        public ModOp ModOp = ModOp.Add;
        public float ModValue;
        public int GrantedTag;
        public EffectAsset[] OnApply;
        public EffectAsset[] OnStack;
        public EffectAsset[] OnPeriod;
        public EffectAsset[] OnExpire;
        public EffectAsset[] OnHurted;
        public EffectAsset[] OnOwnerCast;
        DurationSpec _baked;

        public DurationSpec Bake()
        {
            if (_baked != null) return _baked;
            _baked = new DurationSpec
            {
                BuffId = BuffId,
                Duration = Duration,
                TickInterval = TickInterval,
                MaxStacks = MaxStacks,
                Stack = Stack,
                MutexGroup = MutexGroup,
                Modifiers = ModValue != 0f
                    ? new[] { new Modifier { Attr = ModAttr, Op = ModOp, Value = ModValue } }
                    : Array.Empty<Modifier>(),
                GrantedTags = GrantedTag != 0
                    ? new[] { new TagId(GrantedTag) }
                    : Array.Empty<TagId>(),
                OnApply = BakeList(OnApply),
                OnStack = BakeList(OnStack),
                OnPeriod = BakeList(OnPeriod),
                OnExpire = BakeList(OnExpire),
                OnHurted = BakeList(OnHurted),
                OnOwnerCast = BakeList(OnOwnerCast)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            ClearList(OnApply);
            ClearList(OnStack);
            ClearList(OnPeriod);
            ClearList(OnExpire);
            ClearList(OnHurted);
            ClearList(OnOwnerCast);
        }

        static IEffect[] BakeList(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        static void ClearList(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }

    [Serializable]
    public sealed class TimelineClipAsset
    {
        public float Start;
        public float End;
        public ClipKind Kind;
        public float MoveX;
        public float MoveY;
        public float MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX;
        public float HitOffsetY;
        public float HitOffsetZ;
        public HitProfileAsset HitProfile;
    }

    [Serializable]
    public sealed class TimelinePayloadAsset
    {
        public float Time;
        public EffectAsset[] Effects;
    }

    [CreateAssetMenu(menuName = "Combat/Timeline")]
    public sealed partial class SkillTimelineAsset : ScriptableObject
    {
        public int TimelineIdValue;
        public float Duration = 0.55f;
        public TimelineClipAsset[] Clips;
        public TimelinePayloadAsset[] Payloads;
        TimelineSO _baked;

        public TimelineSO Bake()
        {
            if (_baked != null) return _baked;
            var clipAssets = Clips ?? Array.Empty<TimelineClipAsset>();
            var clips = new TimelineClip[clipAssets.Length];
            for (int i = 0; i < clipAssets.Length; i++)
            {
                var c = clipAssets[i];
                if (c == null) continue;
                clips[i] = new TimelineClip
                {
                    Start = c.Start,
                    End = c.End,
                    Kind = c.Kind,
                    MoveX = c.MoveX,
                    MoveY = c.MoveY,
                    MoveZ = c.MoveZ,
                    Steer = c.Steer,
                    HitRadius = c.HitRadius,
                    HitOffsetX = c.HitOffsetX,
                    HitOffsetY = c.HitOffsetY,
                    HitOffsetZ = c.HitOffsetZ,
                    OnHit = c.HitProfile != null ? c.HitProfile.Bake() : Array.Empty<IEffect>()
                };
            }

            var payloadAssets = Payloads ?? Array.Empty<TimelinePayloadAsset>();
            var payloads = new TimelinePayload[payloadAssets.Length];
            for (int i = 0; i < payloadAssets.Length; i++)
            {
                var p = payloadAssets[i];
                if (p == null) continue;
                payloads[i] = new TimelinePayload { Time = p.Time, Effects = BakeEffects(p.Effects) };
            }

            _baked = new TimelineSO
            {
                Id = new TimelineId(TimelineIdValue),
                Duration = Duration,
                Clips = clips,
                Payloads = payloads
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            if (Clips != null)
                for (int i = 0; i < Clips.Length; i++)
                    if (Clips[i] != null && Clips[i].HitProfile) Clips[i].HitProfile.ClearCache();
            if (Payloads == null) return;
            for (int i = 0; i < Payloads.Length; i++)
                if (Payloads[i] != null) ClearEffects(Payloads[i].Effects);
        }

        static IEffect[] BakeEffects(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        static void ClearEffects(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }

    [CreateAssetMenu(menuName = "Combat/Projectile")]
    public sealed partial class ProjectileDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Speed = 14f;
        public float Lifetime = 2f;
        public float HitRadius = 0.3f;
        public int MaxHits = 1;
        public bool SnapshotAtk = true;
        public int HostileMask;
        public int CueId;
        public float SpawnForward = 0.4f;
        public float HomingRate;
        public float HomingMaxTurn;
        public bool HomingRetarget;
        public float HomingAcquireRadius = 12f;
        public EffectAsset[] OnHit;
        public EffectAsset[] OnExpire;
        ProjectileDefinition _baked;

        public ProjectileDefinition Bake()
        {
            if (_baked != null) return _baked;
            _baked = new ProjectileDefinition
            {
                SpecId = SpecId,
                Speed = Speed,
                Lifetime = Lifetime,
                HitRadius = HitRadius,
                MaxHits = MaxHits,
                SnapshotAtk = SnapshotAtk,
                HostileMask = HostileMask,
                CueId = CueId,
                SpawnForward = SpawnForward,
                HomingRate = HomingRate,
                HomingMaxTurn = HomingMaxTurn,
                HomingRetarget = HomingRetarget,
                HomingAcquireRadius = HomingAcquireRadius,
                OnHit = BakeFx(OnHit),
                OnExpire = BakeFx(OnExpire)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            ClearFx(OnHit);
            ClearFx(OnExpire);
        }

        public static IEffect[] BakeFx(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        public static void ClearFx(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }

    [CreateAssetMenu(menuName = "Combat/Aoe")]
    public sealed partial class AoeDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Radius = 1.3f;
        public float Duration = 2f;
        public float PulseInterval = 0.45f;
        public bool PulseOnSpawn = true;
        public bool TrackOccupancy;
        public int HostileMask;
        public int CueId;
        public EffectAsset[] OnPulse;
        public EffectAsset[] OnEnter;
        public EffectAsset[] OnExit;
        public EffectAsset[] OnStay;
        AoeDefinition _baked;

        public AoeDefinition Bake()
        {
            if (_baked != null) return _baked;
            _baked = new AoeDefinition
            {
                SpecId = SpecId,
                Radius = Radius,
                Duration = Duration,
                PulseInterval = PulseInterval,
                PulseOnSpawn = PulseOnSpawn,
                TrackOccupancy = TrackOccupancy,
                HostileMask = HostileMask,
                CueId = CueId,
                OnPulse = ProjectileDefAsset.BakeFx(OnPulse),
                OnEnter = ProjectileDefAsset.BakeFx(OnEnter),
                OnExit = ProjectileDefAsset.BakeFx(OnExit),
                OnStay = ProjectileDefAsset.BakeFx(OnStay)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            ProjectileDefAsset.ClearFx(OnPulse);
            ProjectileDefAsset.ClearFx(OnEnter);
            ProjectileDefAsset.ClearFx(OnExit);
            ProjectileDefAsset.ClearFx(OnStay);
        }

        void OnValidate() => ClearCache();
    }

    [CreateAssetMenu(menuName = "Combat/Summon")]
    public sealed partial class SummonDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Lifetime;
        public float FollowRange = 2f;
        public float AcquireRadius = 8f;
        // Optional hand-authored graph. When omitted, the compact recipe
        // remains the default so generated V4 content stays immediately
        // runnable.
        public BtNodeAsset Tree;
        public TreeRecipeKind Recipe = TreeRecipeKind.SummonMelee;
        public SummonDefinition Bake()
        {
            return new SummonDefinition
            {
                SpecId = SpecId,
                Lifetime = Lifetime,
                FollowRange = FollowRange,
                AcquireRadius = AcquireRadius,
                Tree = Tree != null ? Tree.Bake() : TreeRecipe.Build(Recipe)
            };
        }

        public void ClearCache() { }
        void OnValidate() => ClearCache();
    }

    [Serializable]
    public sealed class ComboEntryAsset
    {
        public int[] PreSkills;
        public string InputAction = "Attack";
        public int[] RequiredTags;
        public int Priority;
        public int ToSkill;
        public int Timeline;
    }

    [CreateAssetMenu(menuName = "Combat/ComboTable")]
    public sealed partial class ComboTableAsset : ScriptableObject
    {
        public ComboEntryAsset[] Entries;

        public ComboTableSO Bake()
        {
            var source = Entries ?? Array.Empty<ComboEntryAsset>();
            var result = new ComboEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var entry = source[i] ?? new ComboEntryAsset();
                var preValues = entry.PreSkills ?? Array.Empty<int>();
                var pre = new SkillNodeId[preValues.Length];
                for (int j = 0; j < preValues.Length; j++) pre[j] = new SkillNodeId(preValues[j]);
                result[i] = new ComboEntry
                {
                    PreSkills = pre,
                    Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags ?? Array.Empty<int>(),
                    Priority = entry.Priority,
                    ToSkill = new SkillNodeId(entry.ToSkill),
                    Timeline = new TimelineId(entry.Timeline)
                };
            }
            return new ComboTableSO { Entries = result };
        }
    }

    [CreateAssetMenu(menuName = "Combat/Motor")]
    public sealed partial class CharacterMotorAsset : ScriptableObject
    {
        public float Gravity = -20f;
        public float JumpSpeed = 6f;
        public float AirSteer = 0.35f;
        public float GroundY;
        public float StickDeadzone = 0.25f;

        public MotorConfig Bake() => new MotorConfig
        {
            Gravity = Gravity,
            JumpSpeed = JumpSpeed,
            AirSteer = AirSteer,
            GroundY = GroundY,
            StickDeadzone = StickDeadzone
        };
    }

    [CreateAssetMenu(menuName = "Combat/Cues")]
    public sealed partial class CueLibraryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int CueId;
            public string PrefabKey;
            public string SfxKey;
            public float LifeTime;
            public GameObject Prefab;
        }

        public Entry[] Entries;

        public CueLibrary Bake()
        {
            var library = new CueLibrary();
            if (Entries == null) return library;
            for (int i = 0; i < Entries.Length; i++)
            {
                var entry = Entries[i];
                library.Register(new CueDef
                {
                    CueId = entry.CueId,
                    PrefabKey = entry.PrefabKey,
                    SfxKey = entry.SfxKey,
                    LifeTime = entry.LifeTime
                });
            }
            return library;
        }

        public GameObject FindPrefab(int cueId)
        {
            if (Entries == null) return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].CueId == cueId) return Entries[i].Prefab;
            return null;
        }
    }

    [CreateAssetMenu(menuName = "Combat/Database")]
    public sealed partial class CombatDatabaseAsset : ScriptableObject
    {
        public ComboTableAsset Combo;
        public SkillTimelineAsset[] Timelines;
        public ProjectileDefAsset[] Projectiles;
        public AoeDefAsset[] Aoes;
        public DurationSpecAsset[] Buffs;
        public SummonDefAsset[] Summons;
        public CueLibraryAsset Cues;
        public CharacterMotorAsset Motor;

        public void ClearCache()
        {
            ClearArray(Timelines, item => item.ClearCache());
            ClearArray(Projectiles, item => item.ClearCache());
            ClearArray(Aoes, item => item.ClearCache());
            ClearArray(Buffs, item => item.ClearCache());
            ClearArray(Summons, item => item.ClearCache());
        }

        public BakedCombatData BakeAll()
        {
            ClearCache();
            var data = new BakedCombatData
            {
                Combo = Combo != null ? Combo.Bake() : DemoTables.G1G2(),
                Timelines = new TimelineLibrary(),
                Projectiles = new ProjectileCatalog(),
                Aoes = new AoeCatalog(),
                Summons = new SummonCatalog(),
                Cues = Cues != null ? Cues.Bake() : CueLibrary.DefaultCombat(),
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults()
            };
            if (Timelines != null)
                for (int i = 0; i < Timelines.Length; i++) if (Timelines[i]) data.Timelines.Register(Timelines[i].Bake());
            if (Projectiles != null)
                for (int i = 0; i < Projectiles.Length; i++) if (Projectiles[i]) data.Projectiles.Register(Projectiles[i].Bake());
            if (Aoes != null)
                for (int i = 0; i < Aoes.Length; i++) if (Aoes[i]) data.Aoes.Register(Aoes[i].Bake());
            if (Summons != null)
                for (int i = 0; i < Summons.Length; i++) if (Summons[i]) data.Summons.Register(Summons[i].Bake());
            return data;
        }

        static void ClearArray<T>(T[] array, Action<T> clear) where T : ScriptableObject
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++) if (array[i]) clear(array[i]);
        }
    }

    public sealed class SoCombatContent : ICombatContent
    {
        readonly CombatDatabaseAsset _database;
        public SoCombatContent(CombatDatabaseAsset database) => _database = database;
        public BakedCombatData Bake() => _database != null ? _database.BakeAll() : new CodeCombatContent().Bake();
    }

    public enum TreeRecipeKind { None, Puncher, Guard, SummonMelee }

    public static class TreeRecipe
    {
        public static BtNode Build(TreeRecipeKind kind)
        {
            switch (kind)
            {
                case TreeRecipeKind.Puncher: return BtFactory.MeleePuncher(SkillNodeId.G1, TimelineId.TL_G1);
                case TreeRecipeKind.Guard: return BtFactory.MeleeGuard(SkillNodeId.G1, TimelineId.TL_G1);
                case TreeRecipeKind.SummonMelee: return BtFactory.SummonMelee(SkillNodeId.G1, TimelineId.TL_G1);
                default: return null;
            }
        }
    }

    public abstract partial class BtNodeAsset : ScriptableObject
    {
        public abstract BtNode Bake();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Selector")]
    public sealed partial class BtSelectorAsset : BtNodeAsset
    {
        public BtNodeAsset[] Children;
        public override BtNode Bake() => new BtSelector(BakeChildren(Children));
        static BtNode[] BakeChildren(BtNodeAsset[] children)
        {
            if (children == null || children.Length == 0)
                throw new InvalidOperationException("BtSelectorAsset requires at least one child.");
            var source = children;
            var result = new BtNode[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) throw new InvalidOperationException("BtSelectorAsset contains an empty child at index " + i + ".");
                result[i] = source[i].Bake();
            }
            return result;
        }
    }

    [CreateAssetMenu(menuName = "Combat/BT/Sequence")]
    public sealed partial class BtSequenceAsset : BtNodeAsset
    {
        public BtNodeAsset[] Children;
        public override BtNode Bake()
        {
            if (Children == null || Children.Length == 0)
                throw new InvalidOperationException("BtSequenceAsset requires at least one child.");
            var source = Children;
            var result = new BtNode[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) throw new InvalidOperationException("BtSequenceAsset contains an empty child at index " + i + ".");
                result[i] = source[i].Bake();
            }
            return new BtSequence(result);
        }
    }

    [CreateAssetMenu(menuName = "Combat/BT/PlaySkill")]
    public sealed partial class ActPlaySkillAsset : BtNodeAsset
    {
        public int SkillId = SkillNodeId.G1.Value;
        public int TimelineId = Combat.Core.TimelineId.TL_G1.Value;
        public override BtNode Bake() => new ActPlaySkill(new SkillNodeId(SkillId), new Combat.Core.TimelineId(TimelineId));
    }
}
