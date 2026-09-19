using System;
using System.Collections.Generic;
using System.Reflection;
using Combat.Config;
using Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor
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
            // Dry run first: abort before writing anything when some effect has no asset
            // class, so a lossy rebuild can never quietly succeed.
            RequireAllEffectsResolvable(data);
            EnsureFolder(ContentRoot);
            EnsureFolder(ContentRoot + "/Projectiles");
            EnsureFolder(ContentRoot + "/Aoes");
            EnsureFolder(ContentRoot + "/Timelines");
            EnsureFolder(ContentRoot + "/Skills");
            EnsureFolder(ContentRoot + "/Actors");

            var projectiles = new List<ProjectileDefAsset>();
            foreach (var def in data.Projectiles.All)
            {
                var asset = CreateAsset<ProjectileDefAsset>(ContentRoot + "/Projectiles/PD_" + def.SpecId + ".asset");
                ClearSubAssets(asset);
                CopyFields(def, asset);
                asset.SpecId = def.SpecId;
                // OnHit/OnExpire effects are in-memory instances created by ConvertEffects; they
                // only serialise once attached to this asset as sub-assets.
                PersistEffectSubAssets(asset);
                asset.ClearCache();
                EditorUtility.SetDirty(asset);
                projectiles.Add(asset);
            }

            var aoes = new List<AoeDefAsset>();
            foreach (var def in data.Aoes.All)
            {
                var asset = CreateAsset<AoeDefAsset>(ContentRoot + "/Aoes/AD_" + def.SpecId + ".asset");
                ClearSubAssets(asset);
                CopyFields(def, asset);
                asset.SpecId = def.SpecId;
                PersistEffectSubAssets(asset);
                asset.ClearCache();
                EditorUtility.SetDirty(asset);
                aoes.Add(asset);
            }

            var timelines = new List<SkillTimelineAsset>();
            foreach (var so in data.Timelines.All)
            {
                var asset = CreateAsset<SkillTimelineAsset>(ContentRoot + "/Timelines/TL_" + so.Id.Value + ".asset");
                // Rebuilds must be repeatable: drop the sub-assets left by the previous run
                // before attaching fresh ones, or every rebuild accumulates orphans.
                ClearSubAssets(asset);
                asset.TimelineIdValue = so.Id.Value;
                asset.Duration = so.Duration;
                asset.AnimatorState = so.AnimatorState;
                asset.AllowMove = so.AllowMove;
                asset.AllowRotate = so.AllowRotate;
                asset.AllowSkill = so.AllowSkill;
                asset.ScaleWithActionSpeed = so.ScaleWithActionSpeed;
                asset.Clips = BuildClips(so);
                asset.Payloads = BuildPayloads(so);
                // Drop any cached bake: the verification below reads the asset back, and a
                // stale _baked would report the previous (possibly castless) content.
                asset.ClearCache();
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

            // Cue definitions (id -> prefab key / sfx key / lifetime) are generated, but the
            // prefab binding is authored in the Inspector, so a rebuild carries it across.
            var cueAsset = CreateAsset<CueLibraryAsset>(ContentRoot + "/Cues.asset", false);
            var previousCues = cueAsset.Entries;
            var cueEntries = new List<CueLibraryAsset.Entry>();
            foreach (var cue in data.Cues.All)
            {
                int carried = FindCueEntry(previousCues, cue.CueId);
                cueEntries.Add(new CueLibraryAsset.Entry
                {
                    CueId = cue.CueId,
                    PrefabKey = cue.PrefabKey,
                    SfxKey = cue.SfxKey,
                    LifeTime = cue.LifeTime,
                    Prefab = carried >= 0 ? previousCues[carried].Prefab : null,
                    VisualEnabled = carried >= 0 && previousCues[carried].VisualEnabled
                });
            }
            cueAsset.Entries = cueEntries.ToArray();
            EditorUtility.SetDirty(cueAsset);

            var motorAsset = CreateAsset<CharacterMotorAsset>(ContentRoot + "/BA_Motor.asset");
            CopyFields(data.Motor, motorAsset);
            EditorUtility.SetDirty(motorAsset);

            var actors = new List<BuffArenaActorDefAsset>();
            foreach (var def in data.Actors)
            {
                var asset = CreateAsset<BuffArenaActorDefAsset>(
                    ContentRoot + "/Actors/BA_" + def.BlueprintId + ".asset");
                CopyFields(def, asset);
                asset.BlueprintId = def.BlueprintId;
                EditorUtility.SetDirty(asset);
                actors.Add(asset);
            }

            var db = AssetDatabase.LoadAssetAtPath<BuffArenaDatabaseAsset>(DatabasePath);
            if (db == null) { Debug.LogError("Missing " + DatabasePath); return; }
            db.Projectiles = projectiles.ToArray();
            db.Aoes = aoes.ToArray();
            db.Timelines = timelines.ToArray();
            db.Skills = skills.ToArray();
            db.Actors = actors.ToArray();
            db.Seed = data.Seed;
            db.Motor = motorAsset;
            db.Cues = cueAsset;
            EditorUtility.SetDirty(db);

            // Verify BEFORE saving: a lossy conversion must not leave half-written assets on
            // disk, because the runtime is configured to refuse to start on broken content.
            int mismatches = Verify();
            if (mismatches != 0)
                throw new InvalidOperationException("Buff Arena rebuild aborted before saving: "
                    + mismatches + " difference(s) between the freshly built content and the code table. "
                    + "The previous assets are untouched. See the warnings above.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Re-check what actually landed on disk: sub-asset references only survive a
            // save + reimport, so this is the pass that catches a persistence failure.
            int persisted = VerifyPersisted();
            if (persisted != 0)
                Debug.LogError("Buff Arena rebuild wrote assets that do not survive a reimport ("
                    + persisted + " difference(s)). The content on disk is inconsistent — rebuild "
                    + "or restore the assets under Assets/Combat/Config/Generated.");
            Debug.Log("Buff Arena content rebuilt: projectiles=" + projectiles.Count +
                " aoes=" + aoes.Count + " timelines=" + timelines.Count + " skills=" + skills.Count +
                " actors=" + actors.Count + " motor=" + (motorAsset != null) +
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

        /// Attaches every EffectAsset reachable from the asset's public fields, so effect
        /// references survive a domain reload instead of coming back null.
        static void PersistEffectSubAssets(UnityEngine.Object owner)
        {
            var fields = owner.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var value = fields[i].GetValue(owner);
                var single = value as EffectAsset;
                if (single != null)
                {
                    Attach(single, owner);
                    continue;
                }
                var array = value as EffectAsset[];
                if (array == null) continue;
                for (int k = 0; k < array.Length; k++) Attach(array[k], owner);
            }
        }

        /// Removes every sub-asset owned by the given asset, keeping the main object.
        static void ClearSubAssets(UnityEngine.Object owner)
        {
            if (owner == null) return;
            var path = AssetDatabase.GetAssetPath(owner);
            if (string.IsNullOrEmpty(path)) return;
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all == null) return;
            for (int i = 0; i < all.Length; i++)
            {
                var sub = all[i];
                if (sub == null || sub == owner) continue;
                if (AssetDatabase.IsSubAsset(sub)) AssetDatabase.RemoveObjectFromAsset(sub);
            }
        }

        [MenuItem("Combat/Buff Arena/Verify Content Assets")]
        public static void VerifyMenu()
        {
            int differences = Verify();
            if (differences < 0) return;
            if (differences == 0)
                Debug.Log("Buff Arena verify: the generated assets match the code-defined table (0 differences).");
            else
                Debug.LogError("Buff Arena verify: " + differences
                    + " difference(s) between the generated assets and the code-defined table. See the warnings above.");
        }

        /// <summary>
        /// Field-by-field comparison of the generated ScriptableObject content against the
        /// code-defined table. Returns the number of differences, or -1 when the assets could
        /// not be baked at all. This is the gate that lets the runtime depend on the assets.
        /// </summary>
        /// <summary>Differences recorded by the last Verify() call, for tooling and tests.</summary>
        public static List<string> LastDifferences { get; private set; } = new List<string>();

        public static int Verify()
        {
            var db = AssetDatabase.LoadAssetAtPath<BuffArenaDatabaseAsset>(DatabasePath);
            if (db == null)
            {
                Debug.LogError("Buff Arena verify: missing " + DatabasePath);
                return -1;
            }
            var fromSo = db.BakeContentForVerification();
            if (fromSo == null)
            {
                Debug.LogError("Buff Arena verify: the generated content did not bake — "
                    + (string.IsNullOrEmpty(db.LastContentError) ? "unknown reason" : db.LastContentError));
                return -1;
            }
            var fromCode = BuffArenaContent.Build();
            var diffs = new List<string>();
            CompareValue("PlayerMaxHp", fromCode.PlayerMaxHp, fromSo.PlayerMaxHp, diffs);
            CompareValue("PlayerAmmoCapacity", fromCode.PlayerAmmoCapacity, fromSo.PlayerAmmoCapacity, diffs);
            CompareValue("MaxEnemies", fromCode.MaxEnemies, fromSo.MaxEnemies, diffs);
            CompareValue("SpawnPeriod", fromCode.SpawnPeriod, fromSo.SpawnPeriod, diffs);
            CompareValue("EnemyCleanupDelay", fromCode.EnemyCleanupDelay, fromSo.EnemyCleanupDelay, diffs);
            CompareValue("BarrelSelfDamagePeriod", fromCode.BarrelSelfDamagePeriod, fromSo.BarrelSelfDamagePeriod, diffs);
            CompareValue("Seed", fromCode.Seed, fromSo.Seed, diffs);
            CompareValue("Motor", fromCode.Motor, fromSo.Motor, diffs);
            CompareByKey("timeline", "Id", fromCode.Timelines.All, fromSo.Timelines.All, diffs);
            CompareByKey("projectile", "SpecId", fromCode.Projectiles.All, fromSo.Projectiles.All, diffs);
            CompareByKey("aoe", "SpecId", fromCode.Aoes.All, fromSo.Aoes.All, diffs);
            CompareByKey("skill", "Id", fromCode.Skills, fromSo.Skills, diffs);
            CompareByKey("actor", "BlueprintId", fromCode.Actors, fromSo.Actors, diffs);
            CompareByKey("cue", "CueId", fromCode.Cues.All, fromSo.Cues.All, diffs);
            LastDifferences = diffs;
            for (int i = 0; i < diffs.Count; i++)
                Debug.LogWarning("Buff Arena verify diff: " + diffs[i]);
            return diffs.Count;
        }

        /// <summary>
        /// Re-imports the generated content and verifies it again, so references that only exist
        /// in memory (attached sub-assets) cannot pass for working configuration.
        /// </summary>
        static int VerifyPersisted()
        {
            AssetDatabase.ImportAsset(ContentRoot, ImportAssetOptions.ImportRecursive);
            AssetDatabase.ImportAsset(DatabasePath, ImportAssetOptions.ForceUpdate);
            // Baked results are cached on the assets; without clearing them the check would
            // compare the previous in-memory objects and never see a lost reference.
            ClearCachesForVerification();
            return Verify();
        }

        static void ClearCachesForVerification()
        {
            var db = AssetDatabase.LoadAssetAtPath<BuffArenaDatabaseAsset>(DatabasePath);
            if (db == null) return;
            if (db.Projectiles != null)
                for (int i = 0; i < db.Projectiles.Length; i++)
                    if (db.Projectiles[i] != null) db.Projectiles[i].ClearCache();
            if (db.Aoes != null)
                for (int i = 0; i < db.Aoes.Length; i++)
                    if (db.Aoes[i] != null) db.Aoes[i].ClearCache();
            if (db.Timelines != null)
                for (int i = 0; i < db.Timelines.Length; i++)
                    if (db.Timelines[i] != null) db.Timelines[i].ClearCache();
        }

        static void CompareByKey(string label, string keyField, System.Collections.IEnumerable code,
            System.Collections.IEnumerable so, List<string> diffs)
        {
            var mapCode = Index(code, keyField);
            var mapSo = Index(so, keyField);
            foreach (var pair in mapCode)
            {
                object other;
                if (!mapSo.TryGetValue(pair.Key, out other))
                {
                    diffs.Add(label + " " + pair.Key + ": missing from the generated assets");
                    continue;
                }
                CompareValue(label + " " + pair.Key, pair.Value, other, diffs);
            }
            foreach (var pair in mapSo)
                if (!mapCode.ContainsKey(pair.Key))
                    diffs.Add(label + " " + pair.Key + ": extra in the generated assets");
        }

        static Dictionary<string, object> Index(System.Collections.IEnumerable items, string keyField)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            if (items == null) return map;
            foreach (var item in items)
                map[KeyToString(FieldValue(item, keyField))] = item;
            return map;
        }

        static object FieldValue(object target, string fieldName)
        {
            if (target == null) return null;
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field != null ? field.GetValue(target) : null;
        }

        static string KeyToString(object key)
        {
            if (key == null) return "(null)";
            var inner = FieldValue(key, "Value");
            return Convert.ToString(inner ?? key);
        }

        /// Recursively compares two runtime objects of the same type and records every
        /// difference. Effect payloads compare field by field too, because both sides bake to
        /// the same runtime types.
        static void CompareValue(string path, object code, object so, List<string> diffs)
        {
            // Unity serialises a missing string as "" and a missing array as an empty array,
            // so null and empty carry the same content and must not be reported as drift.
            if (IsEmpty(code) && IsEmpty(so)) return;
            if (ReferenceEquals(code, so)) return;
            if (code == null || so == null)
            {
                diffs.Add(path + ": " + Describe(code) + " vs " + Describe(so));
                return;
            }
            var codeType = code.GetType();
            if (codeType != so.GetType())
            {
                diffs.Add(path + ": type " + codeType.Name + " vs " + so.GetType().Name);
                return;
            }
            if (codeType.IsPrimitive || codeType.IsEnum || codeType == typeof(string) || codeType == typeof(decimal))
            {
                if (!code.Equals(so)) diffs.Add(path + ": " + Describe(code) + " vs " + Describe(so));
                return;
            }
            var codeArray = code as Array;
            if (codeArray != null)
            {
                var soArray = (Array)so;
                if (codeArray.Length != soArray.Length)
                {
                    diffs.Add(path + ": length " + codeArray.Length + " vs " + soArray.Length);
                    return;
                }
                for (int i = 0; i < codeArray.Length; i++)
                    CompareValue(path + "[" + i + "]", codeArray.GetValue(i), soArray.GetValue(i), diffs);
                return;
            }
            var codeObject = code as UnityEngine.Object;
            if (codeObject != null)
            {
                if (codeObject != (UnityEngine.Object)so) diffs.Add(path + ": asset reference differs");
                return;
            }
            // Non-public fields matter: effects such as SpawnProjectileEffect keep their
            // configuration in a private field, and comparing only public state let a broken
            // bake pass verification.
            var fields = codeType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsStatic) continue;
                if (typeof(Delegate).IsAssignableFrom(fields[i].FieldType)) continue;
                CompareValue(path + "." + fields[i].Name, fields[i].GetValue(code), fields[i].GetValue(so), diffs);
            }
        }

        static bool IsEmpty(object value)
        {
            if (value == null) return true;
            var text = value as string;
            if (text != null) return text.Length == 0;
            var array = value as Array;
            if (array != null) return array.Length == 0;
            return false;
        }

        static string Describe(object value)
        {
            return value == null ? "null" : value.ToString();
        }

        static void Attach(UnityEngine.Object sub, UnityEngine.Object owner)
        {
            if (sub == null) return;
            if (!UnityEditor.AssetDatabase.Contains(sub))
                UnityEditor.AssetDatabase.AddObjectToAsset(sub, owner);
        }

        static int FindCueEntry(CueLibraryAsset.Entry[] entries, int cueId)
        {
            if (entries == null) return -1;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].CueId == cueId) return i;
            return -1;
        }

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            return CreateAsset<T>(path, true);
        }

        /// <param name="allowFileReset">
        /// False for assets that carry authored data (the cue library keeps the prefab bindings
        /// typed in the Inspector), so an unusable file can never be silently thrown away.
        /// </param>
        static T CreateAsset<T>(string path, bool allowFileReset) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null && !(allowFileReset && HoldsUnloadableSubAssets(path)))
                return existing;
            if (existing != null)
            {
                // A sub-asset whose script went missing loads as null, and ClearSubAssets() — which
                // has to skip nulls — can never remove it. Rewriting the file is the only way to
                // drop the orphans; everything under ContentRoot except the cues is generated.
                AssetDatabase.DeleteAsset(path);
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// True when the asset file holds a sub-asset whose script is missing, which Unity reports
        /// as a null entry in the loaded object list.
        /// </summary>
        static bool HoldsUnloadableSubAssets(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all == null) return false;
            for (int i = 0; i < all.Length; i++)
                if (all[i] == null) return true;
            return false;
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// Copies every instance field whose name and type match on both sides. Runtime effects
        /// often keep their configuration in a private field (_specId); that still maps onto the
        /// asset's public field (SpecId), otherwise the asset silently bakes with a zero spec.
        /// IEffect[] members are converted instead, see ConvertEffects.
        static void CopyFields(object source, object destination)
        {
            var from = source.GetType();
            var to = destination.GetType();
            const BindingFlags TargetFlags = BindingFlags.Public | BindingFlags.Instance;
            const BindingFlags SourceFlags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var targets = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in to.GetFields(TargetFlags))
                if (!targets.ContainsKey(candidate.Name)) targets[candidate.Name] = candidate;

            foreach (var f in from.GetFields(SourceFlags))
            {
                if (f.IsStatic) continue;
                string name = f.Name;
                if (name.Length > 0 && name[0] == '_') name = name.Substring(1);
                FieldInfo g;
                if (!targets.TryGetValue(name, out g)) continue;
                if (g.FieldType == f.FieldType)
                {
                    if (typeof(UnityEngine.Object).IsAssignableFrom(g.FieldType))
                    {
                        // Cross-asset references cannot be copied by reflection; surface it
                        // instead of silently dropping configuration.
                        Debug.LogWarning("BuffArenaDatabaseBuilder: " + to.Name + "." + g.Name +
                            " is a Unity object reference and is not copied from " + from.Name + ".");
                        continue;
                    }
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
            var assetType = ResolveAssetType(type);
            if (assetType == null)
            {
                Debug.LogWarning("BuffArenaDatabaseBuilder: no asset type for effect " + type.Name);
                return null;
            }
            var asset = ScriptableObject.CreateInstance(assetType) as EffectAsset;
            CopyFields(effect, asset);
            return asset;
        }

        /// Effect asset class names are not uniform: DamageEffect keeps its "Effect" suffix
        /// (DamageEffectAsset) while most others drop it (SpawnProjectileEffect ->
        /// SpawnProjectileAsset). Try the exact name first, then the stripped one, then the
        /// explicit alias table for the few irregular cases.
        static Type ResolveAssetType(Type effectType)
        {
            string name = effectType.Name;
            var exact = FindType(EffectAssetTypePrefix + name + "Asset");
            if (exact != null) return exact;

            var effectSuffix = "Effect";
            if (name.EndsWith(effectSuffix, StringComparison.Ordinal))
            {
                var stripped = FindType(
                    EffectAssetTypePrefix + name.Substring(0, name.Length - effectSuffix.Length) + "Asset");
                if (stripped != null) return stripped;
            }

            string alias;
            if (EffectAssetAliases.TryGetValue(name, out alias))
                return FindType(EffectAssetTypePrefix + alias);
            return null;
        }

        const string EffectAssetTypePrefix = "Combat.Config.";

        static readonly Dictionary<string, string> EffectAssetAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The barrel spawner's asset class is named after the barrel, not the effect.
            { "SpawnBuffArenaBarrelEffect", "SpawnBarrelAsset" },
        };

        /// Walks every timeline payload effect (and clip on-hit bag) and throws when any of
        /// them has no asset class. Called before the rebuild writes anything.
        static void RequireAllEffectsResolvable(BuffArenaData data)
        {
            var missing = new SortedSet<string>(StringComparer.Ordinal);
            var unserializable = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var so in data.Timelines.All)
            {
                if (so.Payloads != null)
                {
                    for (int i = 0; i < so.Payloads.Length; i++)
                        CollectUnresolvable(so.Payloads[i].Effects, missing, unserializable);
                }
                if (so.Clips == null) continue;
                for (int i = 0; i < so.Clips.Length; i++)
                    CollectUnresolvable(so.Clips[i].OnHit, missing, unserializable);
            }
            if (missing.Count > 0)
            {
                var names = new List<string>(missing);
                throw new InvalidOperationException(
                    "Buff Arena content rebuild aborted: no EffectAsset subclass resolves for " +
                    string.Join(", ", names.ToArray()) +
                    ". Add the asset class, or an entry in BuffArenaDatabaseBuilder.EffectAssetAliases.");
            }
            if (unserializable.Count > 0)
            {
                var names = new List<string>(unserializable);
                throw new InvalidOperationException(
                    "Buff Arena content rebuild aborted: these effects resolve to an asset class Unity " +
                    "cannot serialize (" + string.Join(", ", names.ToArray()) + "). A ScriptableObject " +
                    "class only gets a MonoScript when it is alone in a .cs file named after it; move " +
                    "the class into its own file. Without one the asset lands on disk with m_Script: 0 " +
                    "and bakes to a null effect after the next domain reload.");
            }
        }

        static void CollectUnresolvable(IEffect[] effects, SortedSet<string> missing, SortedSet<string> unserializable)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] == null) continue;
                var type = effects[i].GetType();
                var assetType = ResolveAssetType(type);
                if (assetType == null)
                {
                    missing.Add(type.Name);
                    continue;
                }
                if (!HasMonoScript(assetType))
                    unserializable.Add(type.Name + " -> " + assetType.Name);
            }
        }

        /// <summary>
        /// Unity mints a MonoScript only for the class whose name matches its .cs file name. A class
        /// without one is created happily in memory, then lands on disk as m_Script: 0 and bakes to
        /// null after the next domain reload — a skill that casts but spawns nothing.
        /// </summary>
        static bool HasMonoScript(Type assetType)
        {
            var probe = ScriptableObject.CreateInstance(assetType);
            if (probe == null) return false;
            var script = UnityEditor.MonoScript.FromScriptableObject(probe);
            bool has = script != null && script.GetClass() == assetType;
            UnityEngine.Object.DestroyImmediate(probe);
            return has;
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
