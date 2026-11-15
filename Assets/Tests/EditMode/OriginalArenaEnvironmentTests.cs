using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class OriginalArenaEnvironmentTests
    {
        static SkillProjectDocument Project()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        static JObject Original()=>JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath,"Tests/Fixtures/OriginalArenaEnvironment.json")));
        static void Equal(JToken expected,JToken actual,string label)
        {var result=SkillReplayComparer.Compare(expected,actual);Assert.IsTrue(result.Matches,label+"\n"+string.Join("\n",result.Differences.Select(d=>d.Path+": "+d.Expected+" != "+d.Actual)));}
        [Test]
        public void ThreeOriginalActorsKeepTheirBaseAttributesGrowthTeamsAndActualPrefabGuids()
        {
            var project=Project();var content=CombatContentCompiler.Compile(project);
            foreach(var row in Original()["actors"]){
                string id=row.Value<string>("id");var actor=content.Runtime.Actors[id];
                var actual=JObject.FromObject(new{id,role=actor.Role,team=actor.Team,hp=actor.Hp,attack=actor.Attack,moveSpeed=actor.MoveSpeed,actionSpeed=actor.ActionSpeed,critRate=actor.CritRate,radius=actor.CollisionRadius,ammo=actor.Ammo,view=actor.View,
                    defense=actor.Defense,shield=actor.Shield,damageDealt=actor.DamageDealt,damageTaken=actor.DamageTaken});
                if(row["acquireRadius"]!=null)actual["acquireRadius"]=actor.AcquireRadius;
                var view=content.Runtime.Views[actor.View];actual["prefabGuid"]=AssetDatabase.AssetPathToGUID("Assets/Resources/"+view.Resource+".prefab");
                var spawn=actor.SpawnStats;var values=JObject.FromObject(new{hpPerSpawn=spawn.HpPerSpawn,attackPerSpawn=spawn.AttackPerSpawn,randomAttack=spawn.AttackRandomRange});
                if(spawn.MoveSpeedCurve!=null){var speed=spawn.MoveSpeedCurve;values["speed"]=JObject.FromObject(new{@base=speed.Base,randomRange=speed.RandomRange,scale=speed.Scale,divisor=speed.Divisor,offset=speed.Offset});}
                actual["spawn"]=values;Equal(row,actual,id);
            }
        }
        [Test]
        public void OriginalRulesMotorAndCameraValuesSurviveCompilation()
        {
            var project=Project();var content=CombatContentCompiler.Compile(project);var arena=content.Runtime.Arena;var motor=content.Runtime.Data.Motor;var source=Original();
            Equal(source["rules"],JObject.FromObject(new{seed=arena.Seed,maxEnemies=arena.MaxEnemies,spawnPeriod=arena.SpawnPeriod,cleanupDelay=arena.CleanupDelay,
                barrelDecayPeriod=(float)project.Skills.Single(s=>s.Id=="arena-barrel").Nodes.Single(n=>n.Kind=="construct").Parameters["decayPeriod"]}),"rules");
            Equal(source["motor"],JObject.FromObject(new{gravity=motor.Gravity,jumpSpeed=motor.JumpSpeed,airSteer=motor.AirSteer,groundY=motor.GroundY,stickDeadzone=motor.StickDeadzone}),"motor");
            var camera=(JObject)source["camera"].DeepClone();foreach(string key in new[]{"light","confirm","heavy","finisher"})camera.Remove(key);
            Equal(camera,JObject.FromObject(new{distance=arena.CameraDistance,height=arena.CameraHeight,fov=arena.CameraFov,standing=arena.CameraFocusHeight,lowered=arena.CameraDownedFocusHeight,springHz=arena.CameraSpringHz,shake=arena.CameraShakeEnabled}),"camera");
        }
        [TestCase("cameraSpringHz",-1)]
        [TestCase("cameraSpringHz",101)]
        [TestCase("cameraDownedFocusHeight",-1)]
        [TestCase("cameraDownedFocusHeight",21)]
        public void InvalidCameraSettingsRejectTheExactField(string field,float value)
        {
            var raw=JObject.Parse(SkillProjectJson.Write(Project()));raw["runtime"]["arena"][field]=value;
            StringAssert.Contains(field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(SkillProjectJson.Read(raw.ToString()))).Path);
        }
        [Test]
        public void OptionalCameraSettingsHaveExplicitDefaultsAndRoundtripIndependentOverrides()
        {
            var p=Project();p.Runtime.Arena.CameraSpringHz=0;p.Runtime.Arena.CameraDownedFocusHeight=.8f;p.Runtime.Arena.CameraShakeEnabled=false;
            var copy=SkillProjectJson.Read(SkillProjectJson.Write(p));var c=CombatContentCompiler.Compile(copy).Runtime.Arena;
            Assert.AreEqual(0,c.CameraSpringHz);Assert.AreEqual(.8,c.CameraDownedFocusHeight,.00001);Assert.IsFalse(c.CameraShakeEnabled);
            p.Runtime.Arena.CameraSpringHz=9;Assert.AreEqual(0,c.CameraSpringHz);
            foreach(string slot in new[]{"f6","f7"})Assert.DoesNotThrow(()=>SkillPublicationJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/"+slot+".json"))));
        }
    }
}
