using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillSnapshotNumberTests
    {
        [TestCase("held-spray")]
        [TestCase("fan-channel")]
        public void IndefiniteChannelSnapshotsContainOnlyFiniteJsonNumbersAndKeepTheActiveAction(string id)
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p))){
                simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=id,X=0,Z=0});
                foreach(int steps in new[]{0,5,20}){
                    if(steps>0)simulation.Advance(steps);var state=simulation.Snapshot();Assert.IsTrue(state.Channeling);
                    var caster=state.Actors.Single(a=>a.Kind=="caster");Assert.IsNotNull(caster.CurrentSkill);Assert.IsNull(caster.ActionDuration);
                    foreach(var value in JObject.FromObject(state).DescendantsAndSelf().OfType<JValue>().Where(v=>v.Type==JTokenType.Float))Assert.IsFalse(double.IsNaN(value.Value<double>())||double.IsInfinity(value.Value<double>()),value.Path);
                }
                using(var replay=new SkillReplayRun(simulation.CaptureReplay(id)))Assert.IsTrue(replay.Comparison.Matches);
            }
        }
    }
}
