using System;
using System.Reflection;
using Combat.Core;
using Newtonsoft.Json.Linq;

namespace Combat.Tests
{
    // Test-only semantic projection. Unknown effects fail instead of disappearing from the audit.
    internal static class OriginalArenaEffectProjection
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Flags).GetValue(value);
        public static JArray Capture(IEffect[] effects,bool self=false)
        {
            var result=new JArray();foreach(var effect in effects??Array.Empty<IEffect>())
            {
                if(effect is TargetEffects)
                {foreach(var child in Capture(Field<IEffect[]>(effect,"_effects"),Field<bool>(effect,"_self")))result.Add(child);continue;}
                JObject value;
                if(effect is DamageEffect damage)
                {
                    value=new JObject{{"kind","damage"},{"coefficient",damage.Coeff},{"flat",damage.Flat},{"canCrit",damage.CanCrit},
                        {"ignoreDefense",damage.IgnoreDef},{"scaleByStacks",damage.ScaleByBuffStacks},{"notifyHurt",damage.FireOnHurted},
                        {"sourceStop",damage.SourceHitstopFrames},{"targetStop",damage.TargetHitstopFrames}};
                    if(damage.CanCrit){value["critChance"]=damage.CritChance;value["critMultiplier"]=damage.CritMul;}
                }
                else if(effect is HurtFeedbackEffect)value=new JObject{{"kind","hurt"},{"requireHpDamage",Field<bool>(effect,"_requireDamage")}};
                else if(effect is ApplyDurationEffect)value=new JObject{{"kind","buff"},{"id",Field<DurationSpec>(effect,"_spec").BuffId},{"stacks",Field<int>(effect,"_stacks")},{"recipient",self?"self":"target"}};
                else if(effect is SpawnProjectileEffect)value=new JObject{{"kind","projectile"},{"id",Field<int>(effect,"_specId")}};
                else if(effect is SpawnAoeEffect)value=new JObject{{"kind","area"},{"id",Field<int>(effect,"_specId")}};
                else if(effect is SpawnConstructEffect)value=new JObject{{"kind","barrel"}};
                else if(effect is RefillAmmoEffect)value=new JObject{{"kind","refill"},{"amount",Field<int>(effect,"_amount")}};
                else if(effect is PullTowardPointEffect)value=new JObject{{"kind","pull"},{"mode",Field<bool>(effect,"_rate")?"rate":"distance"},{"strength",Field<float>(effect,"_distance")},{"center",Field<EffectCenter>(effect,"_center").ToString().ToLowerInvariant()}};
                else if(effect is CuePlaybackEffect)
                {
                    bool tiers=Field<bool>(effect,"_stacksAsTier");
                    value=Cue(Field<int>(effect,"_id"),Field<EffectCenter>(effect,"_center"),Field<string>(effect,"_anchor"),Field<CuePlayback>(effect,"_playback"),Field<string>(effect,"_key"),tiers,tiers?Field<int>(effect,"_maxTier"):0);
                }
                else if(effect is PlayPositionedCueEffect)value=Cue(Field<int>(effect,"_id"),Field<EffectCenter>(effect,"_center"));
                else if(effect is PlayCueEffect)value=Cue(Field<int>(effect,"_cueId"),EffectCenter.Source);
                else throw new InvalidOperationException("Unmapped original Arena effect: "+effect?.GetType().Name);
                result.Add(value);
            }
            return result;
        }
        static JObject Cue(int id,EffectCenter center,string anchor="",CuePlayback playback=CuePlayback.Once,string key="",bool tiers=false,int maximum=0)
            =>new JObject{{"kind","cue"},{"id",id},{"center",center.ToString().ToLowerInvariant()},{"anchor",anchor??""},
                {"playback",playback.ToString().ToLowerInvariant()},{"key",key??""},{"tiers",tiers},{"maxTier",maximum}};
    }
}
