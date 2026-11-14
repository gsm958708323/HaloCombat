using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Combat.Core;
using Newtonsoft.Json.Linq;

namespace Combat.Tests
{
    // Frozen against the actual legacy assets before deletion. These exclusions are metadata
    // added by JSON authoring and the AI prototype; independent actor/AI tests cover the latter.
    internal static class RuntimeMigrationSignature
    {
        public static JObject Capture(BakedCombatData data)
        {
            var timelines=new JObject();foreach(int id in new[]{1001,1002,1003,1004}){data.Timelines.TryGet(new TimelineId(id),out var t);timelines[id.ToString()]=Shape(t);}
            var projectiles=new JObject();foreach(int id in new[]{901,902}){data.Projectiles.TryGet(id,out var d);projectiles[id.ToString()]=Shape(d);}
            var areas=new JObject();foreach(int id in new[]{801,802}){data.Aoes.TryGet(id,out var d);areas[id.ToString()]=Shape(d);}
            data.Summons.TryGet(701,out var summon);
            // The fixture describes the three migrated Attack transitions. Arena input rows
            // added later are validated by Arena tests and must not overwrite these originals.
            var originalCombo=new ComboTableDefinition{Entries=data.Combo.Entries.Where(e=>e.Input==InputToken.Attack).ToArray()};
            // Only these four Cues existed in the migration baseline. Arena Cue values
            // added later are exercised by its actual spawn/death presentation tests.
            var originalCues=new CueLibrary();foreach(int id in new[]{101,102,201,202}){if(!data.Cues.TryGet(id,out var cue))throw new InvalidOperationException("Missing migrated Cue "+id);originalCues.Register(cue);}
            return new JObject{{"combo",Shape(originalCombo)},{"timelines",timelines},{"projectiles",projectiles},{"areas",areas},{"summon",Shape(summon)},{"motor",Shape(data.Motor)},{"cues",Shape(originalCues)}};
        }
        static JToken Shape(object value,int depth=0)
        {
            if(value==null)return JValue.CreateNull();if(depth>24)throw new InvalidOperationException("Signature depth exceeded.");
            Type type=value.GetType();if(type.IsPrimitive||value is string||value is decimal)return new JValue(value);
            if(type.IsEnum)return new JValue(value.ToString());
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            if(value is TargetEffects&&!(bool)type.GetField("_self",flags).GetValue(value))return Shape(type.GetField("_effects",flags).GetValue(value),depth+1);
            if(value is IDictionary map){var result=new JObject();foreach(var key in map.Keys)result[key.ToString()]=Shape(map[key],depth+1);return result;}
            if(value is IEnumerable values)
            {
                var result=new JArray();foreach(var item in values){var token=Shape(item,depth+1);if(item is IEffect&&token is JArray children)foreach(var child in children)result.Add(child);else result.Add(token);}return result;
            }
            var fields=new JObject();if(value is IEffect)fields["type"]=type.Name;
            foreach(var field in type.GetFields(flags))
            {
                // Animation is presentation metadata added after this numeric gameplay fixture;
                // ActorViewContentTests and actual Animator PlayMode tests cover its mapping.
                if(field.IsStatic||new[]{"Name","Key","OriginContentKey","Tree","PresentationKind","ResourceKey","Animation"}.Contains(field.Name))continue;
                if(value is TimelineDefinition&&field.Name=="ControlTags"&&((TagId[])field.GetValue(value)).Length==0)continue;
                if(value is TimelineDefinition&&field.Name=="ScaleWithActionSpeed"&&!(bool)field.GetValue(value))continue;
                if(value is DamageEffect damage&&!damage.CanCrit&&field.Name=="CritChance")continue;
                // Original six-skill fixtures predate independent actor stops and graded
                // feedback. Nonzero values still participate in the strict comparison.
                if(value is DamageEffect&&(field.Name=="SourceHitstopFrames"||field.Name=="TargetHitstopFrames")&&(int)field.GetValue(value)==0)continue;
                if(value is DamageEffect&&field.Name=="Feedback"&&(HitFeedbackLevel)field.GetValue(value)==HitFeedbackLevel.None)continue;
                // The pre-tracking fixture has no tracking metadata; live routing is tested separately.
                if(value is ProjectileDefinition&&field.Name=="TrackingKey"&&string.IsNullOrEmpty((string)field.GetValue(value)))continue;
                // Static baseline definitions predate moving-area/contact extensions;
                // only neutral defaults are excluded, with behavior covered independently.
                if(value is ProjectileDefinition&&field.Name=="SameTargetDelay"&&(float)field.GetValue(value)==0)continue;
                if(value is ProjectileDefinition&&field.Name=="PassInvulnerable"&&!(bool)field.GetValue(value))continue;
                if(value is SpawnAoeEffect&&field.Name=="_forwardOffset"&&(float)field.GetValue(value)==0)continue;
                if(value is AoeDefinition area)
                {
                    if((field.Name=="MoveForward"||field.Name=="AbsorbProjectiles")&&!(bool)field.GetValue(value))continue;
                    if(field.Name=="RemoveOnObstacle"&&(bool)field.GetValue(value))continue;
                    if((field.Name=="MoveSpeed"||field.Name=="ProjectileAbsorbForce"||field.Name=="ProjectileRadiusScale")&&(float)field.GetValue(value)==0)continue;
                    if(field.Name=="OnAbsorb"&&area.OnAbsorb.Length==0)continue;
                }
                // The old circle wrapper used implicit hostility; zero preserves that rule.
                if(value is CircleEffects&&field.Name=="_mask"&&(int)field.GetValue(value)==0)continue;
                fields[field.Name]=Shape(field.GetValue(value),depth+1);
            }
            return fields;
        }
    }
}
