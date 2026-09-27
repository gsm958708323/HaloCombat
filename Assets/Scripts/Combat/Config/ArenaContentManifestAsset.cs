using System;
using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Arena Content Manifest")]
    public sealed class ArenaContentManifestAsset : ScriptableObject
    {
        public ProjectileDefAsset[] Projectiles;
        public AoeDefAsset[] Aoes;
        public SkillTimelineAsset[] Timelines;
        public BuffArenaSkillAsset[] Skills;
        public BuffArenaActorDefAsset[] Actors;
        public ComboTableAsset[] Combos;
        public DurationSpecAsset[] Buffs;
        public CueLibraryAsset Cues;

        public bool HasCompleteContent()
            => HasEntries(Projectiles) && HasEntries(Aoes) && HasEntries(Timelines)
                && HasEntries(Skills) && HasEntries(Actors);

        public bool ValidateIds(out string error)
        {
            if (!ValidateIds(Projectiles, p => p.SpecId, "projectile", out error)) return false;
            if (!ValidateIds(Aoes, a => a.SpecId, "aoe", out error)) return false;
            if (!ValidateIds(Timelines, t => t.TimelineIdValue, "timeline", out error)) return false;
            if (!ValidateIds(Skills, s => s.SkillIdValue, "skill", out error)) return false;
            if (!ValidateIds(Buffs, b => b.BuffId, "buff", out error)) return false;
            var blueprints = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < (Actors ?? Array.Empty<BuffArenaActorDefAsset>()).Length; i++)
            {
                var actor = Actors[i];
                if (actor == null || string.IsNullOrEmpty(actor.BlueprintId))
                { error = "actor[" + i + "] has no BlueprintId"; return false; }
                if (!blueprints.Add(actor.BlueprintId))
                { error = "duplicate actor BlueprintId " + actor.BlueprintId; return false; }
            }
            error = null;
            return true;
        }

        public bool ValidateContent(out string error)
        {
            if (!ValidateIds(out error)) return false;
            var projectiles = new HashSet<ProjectileDefAsset>(Projectiles ?? Array.Empty<ProjectileDefAsset>());
            var aoes = new HashSet<AoeDefAsset>(Aoes ?? Array.Empty<AoeDefAsset>());
            var timelines = new HashSet<SkillTimelineAsset>(Timelines ?? Array.Empty<SkillTimelineAsset>());
            var skills = new HashSet<BuffArenaSkillAsset>(Skills ?? Array.Empty<BuffArenaSkillAsset>());
            var buffs = new HashSet<DurationSpecAsset>(Buffs ?? Array.Empty<DurationSpecAsset>());
            var combos = new HashSet<ComboTableAsset>(Combos ?? Array.Empty<ComboTableAsset>());
            if (Cues == null) { error = "Manifest.Cues is null"; return false; }
            for (int i = 0; i < (Combos ?? Array.Empty<ComboTableAsset>()).Length; i++)
                if (Combos[i] == null) { error = "combo[" + i + "] is null"; return false; }

            for (int i = 0; i < (Skills ?? Array.Empty<BuffArenaSkillAsset>()).Length; i++)
            {
                var skill = Skills[i];
                if (skill.Timeline == null) { error = "Skill " + skill.SkillIdValue + " -> Timeline is null"; return false; }
                if (!timelines.Contains(skill.Timeline)) { error = "Skill " + skill.SkillIdValue + " -> Timeline is outside Arena manifest"; return false; }
                if (skill.WarpSkill != null && !skills.Contains(skill.WarpSkill)) { error = "Skill " + skill.SkillIdValue + " -> WarpSkill is outside Arena manifest"; return false; }
                if (skill.FallbackSkill != null && !skills.Contains(skill.FallbackSkill)) { error = "Skill " + skill.SkillIdValue + " -> FallbackSkill is outside Arena manifest"; return false; }
            }

            for (int i = 0; i < (Timelines ?? Array.Empty<SkillTimelineAsset>()).Length; i++)
            {
                var timeline = Timelines[i];
                if (timeline.Duration <= 0f) { error = "Timeline " + timeline.TimelineIdValue + " has non-positive Duration"; return false; }
                for (int j = 0; j < (timeline.Clips ?? Array.Empty<TimelineClipAsset>()).Length; j++)
                {
                    var clip = timeline.Clips[j];
                    if (clip == null) { error = "Timeline " + timeline.TimelineIdValue + " -> Clip[" + j + "] is null"; return false; }
                    if (clip.End <= clip.Start) { error = "Timeline " + timeline.TimelineIdValue + " -> Clip[" + j + "] has End <= Start"; return false; }
                    if (clip.HitProfile != null)
                    {
                        if (!ValidateHitProfile(clip.HitProfile, "Timeline " + timeline.TimelineIdValue + " -> Clip[" + j + "]", projectiles, aoes, buffs, out error)) return false;
                    }
                }
                for (int j = 0; j < (timeline.Payloads ?? Array.Empty<TimelinePayloadAsset>()).Length; j++)
                {
                    var payload = timeline.Payloads[j];
                    if (payload == null) { error = "Timeline " + timeline.TimelineIdValue + " -> Payload[" + j + "] is null"; return false; }
                    if (payload.Time < 0f || payload.Time > timeline.Duration) { error = "Timeline " + timeline.TimelineIdValue + " -> Payload[" + j + "] is outside duration"; return false; }
                    if (!ValidateEffects(payload.Effects, "Timeline " + timeline.TimelineIdValue + " -> Payload[" + j + "]", projectiles, aoes, buffs, out error)) return false;
                }
            }

            for (int i = 0; i < (Actors ?? Array.Empty<BuffArenaActorDefAsset>()).Length; i++)
            {
                var combo = Actors[i].ComboTable;
                if (combo == null)
                {
                    if (Actors[i].BlueprintId == BuffArenaIds.PlayerBlueprint)
                    { error = "Actor " + Actors[i].BlueprintId + " -> ComboTable is null"; return false; }
                    continue;
                }
                if (!combos.Contains(combo)) { error = "Actor " + Actors[i].BlueprintId + " -> ComboTable is outside Arena manifest"; return false; }
                var entries = combo.Entries ?? Array.Empty<ComboEntryAsset>();
                for (int j = 0; j < entries.Length; j++)
                {
                    if (entries[j] == null || entries[j].Skill == null) { error = "Combo " + combo.name + " -> Entry[" + j + "] has no target Skill"; return false; }
                    if (!skills.Contains(entries[j].Skill)) { error = "Combo " + combo.name + " -> Entry[" + j + "] Skill is outside Arena manifest"; return false; }
                    var pres = entries[j].PreSkillAssets ?? Array.Empty<BuffArenaSkillAsset>();
                    for (int k = 0; k < pres.Length; k++)
                        if (pres[k] == null || !skills.Contains(pres[k])) { error = "Combo " + combo.name + " -> Entry[" + j + "] PreSkill[" + k + "] is outside Arena manifest"; return false; }
                }
            }

            for (int i = 0; i < (Projectiles ?? Array.Empty<ProjectileDefAsset>()).Length; i++)
                if (!ValidateEffects(Projectiles[i].OnHit, "Projectile " + Projectiles[i].SpecId + " -> OnHit", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Projectiles[i].OnExpire, "Projectile " + Projectiles[i].SpecId + " -> OnExpire", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Projectiles[i].OnObstacle, "Projectile " + Projectiles[i].SpecId + " -> OnObstacle", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Projectiles[i].OnOwnerHit, "Projectile " + Projectiles[i].SpecId + " -> OnOwnerHit", projectiles, aoes, buffs, out error)) return false;
            for (int i = 0; i < (Aoes ?? Array.Empty<AoeDefAsset>()).Length; i++)
                if (!ValidateEffects(Aoes[i].OnPulse, "AoE " + Aoes[i].SpecId + " -> OnPulse", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Aoes[i].OnEnter, "AoE " + Aoes[i].SpecId + " -> OnEnter", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Aoes[i].OnExit, "AoE " + Aoes[i].SpecId + " -> OnExit", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Aoes[i].OnStay, "AoE " + Aoes[i].SpecId + " -> OnStay", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(Aoes[i].OnExpire, "AoE " + Aoes[i].SpecId + " -> OnExpire", projectiles, aoes, buffs, out error)) return false;
            for (int i = 0; i < (Buffs ?? Array.Empty<DurationSpecAsset>()).Length; i++)
            {
                var buff = Buffs[i];
                if (buff.Duration < 0f || buff.TickInterval < 0f || buff.MaxStacks <= 0)
                { error = "Buff " + buff.BuffId + " has invalid duration, tick interval or MaxStacks"; return false; }
                if (!ValidateEffects(buff.OnApply, "Buff " + buff.BuffId + " -> OnApply", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(buff.OnStack, "Buff " + buff.BuffId + " -> OnStack", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(buff.OnPeriod, "Buff " + buff.BuffId + " -> OnPeriod", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(buff.OnExpire, "Buff " + buff.BuffId + " -> OnExpire", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(buff.OnHurted, "Buff " + buff.BuffId + " -> OnHurted", projectiles, aoes, buffs, out error) ||
                    !ValidateEffects(buff.OnOwnerCast, "Buff " + buff.BuffId + " -> OnOwnerCast", projectiles, aoes, buffs, out error)) return false;
            }
            error = null;
            return true;
        }

        static bool ValidateEffects(EffectAsset[] effects, string path, HashSet<ProjectileDefAsset> projectiles,
            HashSet<AoeDefAsset> aoes, HashSet<DurationSpecAsset> buffs, out string error)
        {
            for (int i = 0; i < (effects ?? Array.Empty<EffectAsset>()).Length; i++)
            {
                var effect = effects[i];
                if (effect == null) { error = path + "[" + i + "] is null"; return false; }
                if (effect is SpawnProjectileAsset spawnProjectile && spawnProjectile.Projectile == null)
                { error = path + "[" + i + "] SpawnProjectile.Projectile is null"; return false; }
                if (effect is SpawnProjectileAsset projectileEffect && !projectiles.Contains(projectileEffect.Projectile))
                { error = path + "[" + i + "] SpawnProjectile.Projectile is outside Arena manifest"; return false; }
                if (effect is SpawnAoeAsset spawnAoe && spawnAoe.Aoe == null)
                { error = path + "[" + i + "] SpawnAoe.Aoe is null"; return false; }
                if (effect is SpawnAoeAsset aoeEffect && !aoes.Contains(aoeEffect.Aoe))
                { error = path + "[" + i + "] SpawnAoe.Aoe is outside Arena manifest"; return false; }
                if (effect is ApplyDurationAsset duration && duration.Spec == null)
                { error = path + "[" + i + "] ApplyDuration.Spec is null"; return false; }
                if (effect is ApplyDurationAsset durationEffect && !buffs.Contains(durationEffect.Spec))
                { error = path + "[" + i + "] ApplyDuration.Spec is outside Arena manifest"; return false; }
                if (effect is SpawnSummonAsset summon && summon.Summon == null)
                { error = path + "[" + i + "] SpawnSummon.Summon is null"; return false; }
                if (effect is AfterDamageAsset afterDamage && !ValidateEffects(afterDamage.Effects, path + "[" + i + "] AfterDamage", projectiles, aoes, buffs, out error))
                    return false;
            }
            error = null;
            return true;
        }

        static bool ValidateHitProfile(HitProfileAsset profile, string path, HashSet<ProjectileDefAsset> projectiles,
            HashSet<AoeDefAsset> aoes, HashSet<DurationSpecAsset> buffs, out string error)
        {
            var effects = new List<EffectAsset>(5);
            if (profile.Damage != null) effects.Add(profile.Damage);
            if (profile.Stun != null) effects.Add(profile.Stun);
            if (profile.Knockback != null) effects.Add(profile.Knockback);
            if (profile.Launch != null) effects.Add(profile.Launch);
            if (profile.IFrame != null) effects.Add(profile.IFrame);
            if (profile.Effects != null) effects.AddRange(profile.Effects);
            return ValidateEffects(effects.ToArray(), path + " -> HitProfile", projectiles, aoes, buffs, out error);
        }

        static bool ValidateIds<T>(T[] items, Func<T, int> id, string label, out string error)
            where T : UnityEngine.Object
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < (items ?? Array.Empty<T>()).Length; i++)
            {
                if (items[i] == null) { error = label + "[" + i + "] is null"; return false; }
                var value = id(items[i]);
                if (value == 0) { error = label + "[" + i + "] has id 0"; return false; }
                if (!seen.Add(value)) { error = "duplicate " + label + " id " + value; return false; }
            }
            error = null;
            return true;
        }

        static bool HasEntries<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null || items.Length == 0) return false;
            for (int i = 0; i < items.Length; i++) if (items[i] == null) return false;
            return true;
        }
    }
}
