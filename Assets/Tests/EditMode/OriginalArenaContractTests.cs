using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class OriginalArenaContractTests
    {
        static SkillProjectDocument Project()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static JObject Reference()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Tests/Fixtures/OriginalArenaContract.json")));
        static JArray Effects(IEffect[] effects)=>OriginalArenaEffectProjection.Capture(effects);
        static void Equal(JToken expected,JToken actual,string label)
        {
            var comparison=SkillReplayComparer.Compare(expected,actual);
            Assert.IsTrue(comparison.Matches,label+"\n"+string.Join("\n",comparison.Differences.Select(d=>d.Path+": expected "+d.Expected+"; actual "+d.Actual)));
        }
        [Test]
        public void ThirteenSkillsKeepTheirRuntimeIdentityTimelineAmmunitionAndFallback()
        {
            var source=Reference();var c=CombatContentCompiler.Compile(Project());Assert.AreEqual(13,((JArray)source["skills"]).Count);
            foreach(var row in source["skills"])
            {
                int id=row.Value<int>("id");Assert.IsTrue(c.Skills.TryGet(new SkillNodeId(id),out var s));
                Equal(row,JObject.FromObject(new{id=s.Id.Value,timeline=s.Timeline.Value,ammo=s.AmmoCost,fallback=s.AmmoFallback.Value}),"SK"+id);
            }
        }
        [Test]
        public void TwelveOriginalAxesKeepEveryPayloadTimeEffectOrderClipAndControl()
        {
            var c=CombatContentCompiler.Compile(Project());foreach(var row in Reference()["timelines"])
            {
                int id=row.Value<int>("id");Assert.IsTrue(c.Timelines.TryGet(new TimelineId(id),out var t));
                var actual=JObject.FromObject(new{id=t.Id.Value,duration=t.Duration,animation=t.Animation,controls=t.ControlTags.Select(tag=>tag.Value).ToArray(),scale=t.ScaleWithActionSpeed,
                    clips=t.Clips.Select(v=>new{kind=(int)v.Kind,start=v.Start,end=v.End,moveX=v.MoveX,moveY=v.MoveY,moveZ=v.MoveZ,steer=v.Steer}).ToArray()});
                actual["payloads"]=new JArray(t.Payloads.Select(p=>new JObject{{"time",p.Time},{"effects",Effects(p.Effects)}}));Equal(row,actual,"TL"+id);
            }
        }
        [Test]
        public void SevenOriginalProjectilesKeepParametersAndOrderedContactLifetimeWallAndReturnBags()
        {
            var c=CombatContentCompiler.Compile(Project());foreach(var row in Reference()["projectiles"])
            {
                int id=row.Value<int>("id");Assert.IsTrue(c.Projectiles.TryGet(id,out var p));Assert.IsTrue(p.PassInvulnerable,"PD"+id+" must preserve immune-contact pass-through.");
                var actual=JObject.FromObject(new{id=p.SpecId,speed=p.Speed,lifetime=p.Lifetime,radius=p.HitRadius,maxHits=(((PierceEffect)System.Array.Find(p.OnHit,e=>e is PierceEffect))?.MaxHits??1),spawnForward=p.SpawnForward,sameTargetDelay=p.SameTargetDelay,
                    removeOnObstacle=p.RemoveOnObstacle,view=p.View,homingRate=p.HomingRate,homingRadius=p.HomingAcquireRadius});
                actual["hit"]=Effects(p.OnHit.Where(e=>!(e is PierceEffect)).ToArray());actual["lifetimeEffects"]=Effects(p.OnExpire);actual["wall"]=Effects(p.OnWall);actual["returned"]=Effects(p.OnReturned);
                Equal(row,actual,"PD"+id);
            }
        }
        [Test]
        public void FourOriginalBuffsRetainIndependentNumbersTimingStackingAndLifecycleEffects()
        {
            var c=CombatContentCompiler.Compile(Project());foreach(var row in Reference()["buffs"])
            {
                int id=row.Value<int>("id");Assert.IsTrue(c.Buffs.TryGet(id,out var b));
                var actual=JObject.FromObject(new{id=b.BuffId,duration=b.Duration,interval=b.TickInterval,maxStacks=b.MaxStacks,stack=(int)b.Stack});
                actual["apply"]=Effects(b.OnApply);actual["period"]=Effects(b.OnPeriod);actual["removed"]=Effects(b.OnRemoved);Equal(row,actual,"BU"+id);
            }
        }
        [Test]
        public void AllSixOriginalAreasKeepOccupancyMotionAbsorptionAndAllLifecycleBags()
        {
            var c=CombatContentCompiler.Compile(Project());foreach(var row in Reference()["areas"])
            {
                int id=row.Value<int>("id");Assert.IsTrue(c.Aoes.TryGet(id,out var a));
                var actual=JObject.FromObject(new{id=a.SpecId,radius=a.Radius,duration=a.Duration,interval=a.PulseInterval,pulseOnSpawn=a.PulseOnSpawn,occupancy=a.TrackOccupancy,
                    moveSpeed=a.MoveSpeed,absorb=a.AbsorbProjectiles,force=a.ProjectileAbsorbForce,radiusScale=a.ProjectileRadiusScale,view=a.View??""});
                actual["pulse"]=Effects(a.OnPulse);actual["enter"]=Effects(a.OnEnter);actual["stay"]=Effects(a.OnStay);actual["exit"]=Effects(a.OnExit);actual["expire"]=Effects(a.OnExpire);Equal(row,actual,"AD"+id);
            }
        }
        [Test]
        public void ActualArenaBindingsResolveExactlyTheTwelveHistoricalComboRows()
        {
            var p=Project();var c=CombatContentCompiler.Compile(p);var bindings=p.Runtime.Arena.Bindings.ToDictionary(b=>b.Input,b=>b.Action);
            var actual=new JArray(c.Runtime.Data.Combo.Entries.Where(e=>bindings.ContainsKey(e.Input.Action)&&e.ToSkill.Value>=2001&&e.ToSkill.Value<=2013&&e.PreSkills.All(s=>s.Value>=2001&&s.Value<=2013))
                .Select(e=>JObject.FromObject(new{pre=e.PreSkills.Select(v=>v.Value).ToArray(),action=bindings[e.Input.Action],tags=e.RequiredTags,priority=e.Priority,target=e.ToSkill.Value})));
            string Key(JToken v)=>string.Join(",",v["pre"].Values<int>())+"/"+v.Value<string>("action")+"/"+v.Value<int>("priority");
            Equal(new JArray(Reference()["combos"].OrderBy(Key)),new JArray(actual.OrderBy(Key)),"Original input table");
        }
    }
}
