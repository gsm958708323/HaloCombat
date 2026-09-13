using System;
using System.Collections.Generic;
using System.Reflection;
using Combat.Config;
using Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    /// One-shot migration helper: reads the code-defined Buff Arena table and writes it
    /// out as ScriptableObject assets under Generated/Content. Definition values are
    /// copied by reflection over matching public field names, so the tables are not
    /// duplicated here. Run "Combat/Buff Arena/Rebuild Content Assets".
    public static class BuffArenaDatabaseBuilder
    {
        const string ContentRoot = "Assets/Combat/Config/Generated/Content";
        const string DatabasePath = "Assets/Combat/Config/Generated/BuffArenaDatabase.asset";

        [MenuItem("Combat/Buff Arena/Rebuild Content Assets")]
        public static void Rebuild()
        {
            var data = BuffArenaContent.Build();
            EnsureFolder(ContentRoot);
            EnsureFolder(ContentRoot + "/Projectiles");
            EnsureFolder(ContentRoot + "/Aoes");
            EnsureFolder(ContentRoot + "/Timelines");
            EnsureFolder(ContentRoot + "/Skills");

            var projectiles = new List<ProjectileDefAsset>();
            foreach (var def in data.Projectiles.All)
            {
                var asset = CreateAsset<ProjectileDefAsset>(ContentRoot + "/Projectiles/PD_" + def.SpecId + ".asset");
                CopyFields(def, asset);
                asset.SpecId = def.SpecId;
                asset.ClearCache();
                EditorUtility.SetDirty(asset);
                projectiles.Add(asset);
            }

            var aoes = new List<AoeDefAsset>();
            foreach (var def in data.Aoes.All)
            {
                var asset = CreateAsset<AoeDefAsset>(ContentRoot + "/Aoes/AD_" + def.SpecId + ".asset");
                CopyFields(def, asset);
                asset.SpecId = def.SpecId;
                asset.ClearCache();
                EditorUtility.SetDirty(asset);
                aoes.Add(asset);
            }

            var timelines = new List<SkillTimelineAsset>();
            foreach (var so in data.Timelines.All)
            {
                var asset = CreateAsset<SkillTimelineAsset>(ContentRoot + "/Timelines/TL_" + so.Id.Value + ".asset");
                asset.TimelineIdValue = so.Id.Value;
                asset.Duration = so.Duration;
                asset.AnimatorState = so.AnimatorState;
                asset.AllowMove = so.AllowMove;
                asset.AllowRotate = so.AllowRotate;
                asset.AllowSkill = so.AllowSkill;
                asset.ScaleWithActionSpeed = so.ScaleWithActionSpeed;
                asset.Clips = BuildClips(so);
                asset.Payloads = BuildPayloads(so);
                // Effect assets produced above are in-memory instances, so they have to be
                // attached to the timeline asset or the reference cannot be serialized.
                PersistSubAssets(asset, asset.Clips, asset.Payloads);
                EditorUtility.SetDirty(asset);
                timelines.Add(asset);
            }

            var skills = new List<BuffArenaSkillAsset>();
            foreach (var skill in data.Skills)
            {
                var asset = CreateAsset<BuffArenaSkillAsset>(ContentRoot + "/Skills/SK_" + skill.Id.Value + ".asset");
                asset.SkillIdValue = skill.Id.Value;
                asset.InputToken = skill.Input.ToString();
                asset.TimelineIdValue = skill.Timeline.Value;
                asset.AmmoCost = skill.AmmoCost;
                asset.AnimatorState = skill.AnimatorState;
                asset.RequiresTrackedProjectile = skill.RequiresTrackedProjectile;
                asset.WarpSkillIdValue = skill.WarpSkillId.Value;
                EditorUtility.SetDirty(asset);
                skills.Add(asset);
            }

            var db = AssetDatabase.LoadAssetAtPath<BuffArenaDatabaseAsset>(DatabasePath);
            if (db == null) { Debug.LogError("Missing " + DatabasePath); return; }
            db.Projectiles = projectiles.ToArray();
            db.Aoes = aoes.ToArray();
            db.Timelines = timelines.ToArray();
            db.Skills = skills.ToArray();
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Buff Arena content rebuilt: projectiles=" + projectiles.Count +
                " aoes=" + aoes.Count + " timelines=" + timelines.Count + " skills=" + skills.Count +
                " complete=" + db.HasCompleteContent());
        }

        static TimelineClipAsset[] BuildClips(TimelineSO so)
        {
            var clips = so.Clips ?? Array.Empty<TimelineClip>();
            if (clips.Length == 0) return Array.Empty<TimelineClipAsset>();
            var result = new TimelineClipAsset[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                result[i] = new TimelineClipAsset
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
                    HitProfile = BuildHitProfile(c.OnHit)
                };
            }
            return result;
        }

        /// The hitbox payload is a single DamageEffect in every Arena timeline, so it is
        /// folded back into the HitProfileAsset the timeline asset expects.
        static HitProfileAsset BuildHitProfile(IEffect[] onHit)
        {
            if (onHit == null || onHit.Length == 0) return null;
            DamageEffectAsset damage = null;
            for (int i = 0; i < onHit.Length; i++)
            {
                if (!(onHit[i] is DamageEffect)) continue;
                damage = ConvertEffect(onHit[i]) as DamageEffectAsset;
                break;
            }
            if (damage == null) return null;
            var profile = ScriptableObject.CreateInstance<HitProfileAsset>();
            profile.Damage = damage;
            profile.ClearCache();
            return profile;
        }

        static TimelinePayloadAsset[] BuildPayloads(TimelineSO so)
        {
            var payloads = so.Payloads ?? Array.Empty<TimelinePayload>();
            if (payloads.Length == 0) return Array.Empty<TimelinePayloadAsset>();
            var result = new TimelinePayloadAsset[payloads.Length];
            for (int i = 0; i < payloads.Length; i++)
            {
                var p = payloads[i];
                var effects = p.Effects ?? Array.Empty<IEffect>();
                var converted = new EffectAsset[effects.Length];
                for (int k = 0; k < effects.Length; k++) converted[k] = ConvertEffect(effects[k]);
                result[i] = new TimelinePayloadAsset { Time = p.Time, Effects = converted };
            }
            return result;
        }

        /// Attaches the in-memory clip/payload effect assets to their timeline asset as
        /// sub-assets so Unity can persist the references from the parent.
        static void PersistSubAssets(SkillTimelineAsset owner, TimelineClipAsset[] clips, TimelinePayloadAsset[] payloads)
        {
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var profile = clips[i] != null ? clips[i].HitProfile : null;
                    if (profile == null) continue;
                    if (profile.Damage != null) Attach(profile.Damage, owner);
                    Attach(profile, owner);
                }
            }
            if (payloads != null)
            {
                for (int i = 0; i < payloads.Length; i++)
                {
                    var effects = payloads[i] != null ? payloads[i].Effects : null;
                    if (effects == null) continue;
                    for (int k = 0; k < effects.Length; k++) Attach(effects[k], owner);
                }
            }
        }

        static void Attach(UnityEngine.Object sub, UnityEngine.Object owner)
        {
            if (sub == null) return;
            if (!UnityEditor.AssetDatabase.Contains(sub))
                UnityEditor.AssetDatabase.AddObjectToAsset(sub, owner);
        }

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// Copies every public instance field whose name and type match on both sides.
        /// IEffect[] members are converted instead, see CopyEffectArray.
        static void CopyFields(object source, object destination)
        {
            var from = source.GetType();
            var to = destination.GetType();
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            foreach (var f in from.GetFields(Flags))
            {
                var g = to.GetField(f.Name, Flags);
                if (g == null) continue;
                if (g.FieldType == f.FieldType)
                {
                    if (typeof(UnityEngine.Object).IsAssignableFrom(g.FieldType)) continue;
                    g.SetValue(destination, f.GetValue(source));
                    continue;
                }
                if ((f.FieldType == typeof(IEffect[]) || f.FieldType == typeof(IEffect)) &&
                    (g.FieldType == typeof(EffectAsset[]) || g.FieldType == typeof(EffectAsset)))
                {
                    g.SetValue(destination, ConvertEffects(f.GetValue(source), g.FieldType));
                }
            }
        }

        static object ConvertEffects(object source, Type targetType)
        {
            var array = targetType == typeof(EffectAsset[]) ? new EffectAsset[0] : (object)null;
            var items = source as IEffect[];
            if (items == null)
            {
                var single = source as IEffect;
                if (single == null) return targetType == typeof(EffectAsset[]) ? (object)Array.Empty<EffectAsset>() : null;
                var one = ConvertEffect(single);
                if (targetType == typeof(EffectAsset[])) return one == null ? Array.Empty<EffectAsset>() : new[] { one };
                return one;
            }
            var result = new EffectAsset[items.Length];
            for (int i = 0; i < items.Length; i++) result[i] = ConvertEffect(items[i]);
            return targetType == typeof(EffectAsset[]) ? (object)result : (result.Length > 0 ? result[0] : null);
        }

        /// Runtime effects have no ScriptableObject counterpart of their own, so the asset
        /// subclass is created and its matching public fields copied across.
        static EffectAsset ConvertEffect(IEffect effect)
        {
            if (effect == null) return null;
            var type = effect.GetType();
            var extraSuffix = type == typeof(DamageEffect) ? "Asset" : string.Empty;
            var assetType = FindType("Combat.Config." + type.Name + extraSuffix + "Asset");
            if (assetType == null)
            {
                Debug.LogWarning("BuffArenaDatabaseBuilder: no asset type for effect " + type.Name);
                return null;
            }
            var asset = ScriptableObject.CreateInstance(assetType) as EffectAsset;
            CopyFields(effect, asset);
            return asset;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
