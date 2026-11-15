using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillReplayValidationTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static JObject FileToken()
        {
            var p=Example();using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0});s.Advance(1);
                s.Execute(new SkillSimulationCommand{Kind="cancel"});return JObject.Parse(SkillReplayJson.Write(s.CaptureReplay("basic-shot")));
            }
        }
        [TestCase("{\"kind\":\"step\",\"steps\":1.5}")]
        [TestCase("{\"kind\":\"cast\",\"skillId\":123}")]
        [TestCase("{\"kind\":\"aim\",\"x\":\"3\",\"z\":0}")]
        [TestCase("{\"kind\":\"aim\",\"x\":null,\"z\":0}")]
        [TestCase("{\"kind\":\"aim\",\"x\":0}")]
        [TestCase("{\"kind\":\"move\"}")]
        [TestCase("{\"kind\":\"cancel\",\"skillId\":null}")]
        [TestCase("{\"kind\":\"step\",\"steps\":501}")]
        [TestCase("{\"kind\":\"hitstop\",\"frames\":-1}")]
        [TestCase("{\"kind\":\"remove-buff\",\"actorId\":\"1\",\"instanceId\":0}")]
        [TestCase("{\"kind\":\"snapshot\",\"kind\":\"cancel\"}")]
        [TestCase("{\"kind\":\"unknown\"}")]
        public void CommandsRejectCoercionUnknownFieldsAndInvalidRanges(string json)
            =>Assert.Throws<ContentValidationException>(()=>SkillSimulationCommands.Read(json));

        [TestCase("version")] [TestCase("clock")] [TestCase("end")] [TestCase("order")]
        [TestCase("null-input")] [TestCase("null-command")] [TestCase("fractional-step")]
        [TestCase("fractional-seed")] [TestCase("root")] [TestCase("nested-step")]
        [TestCase("overflow-expected")] [TestCase("nan")]
        public void CorruptReplayIsRejectedWithAValidationPath(string corruption)
        {
            var file=FileToken();
            switch(corruption)
            {
                case "version":file["formatVersion"]=2;break;
                case "clock":file["stepSeconds"]=.03;break;
                case "end":file["endStep"]=30001;break;
                case "order":file["inputs"][0]["atStep"]=1;file["inputs"][1]["atStep"]=0;break;
                case "null-input":file["inputs"][0]=JValue.CreateNull();break;
                case "null-command":file["inputs"][0]["command"]=JValue.CreateNull();break;
                case "fractional-step":file["inputs"][0]["atStep"]=.5;break;
                case "fractional-seed":file["project"]["preview"]["seed"]=1.5;break;
                case "root":file["rootSkillId"]="missing";break;
                case "nested-step":file["inputs"][0]["command"]=JObject.Parse("{\"kind\":\"step\",\"steps\":1}");break;
                case "overflow-expected":file["expectedState"]["step"]=long.MaxValue;break;
                case "nan":file["expectedState"]["time"]=double.NaN;break;
            }
            string text=file.ToString();if(corruption=="nan")text=text.Replace("\"NaN\"","NaN");
            var error=Assert.Throws<ContentValidationException>(()=>SkillReplayJson.Read(text));Assert.IsTrue(error.Path.StartsWith("$"));
        }

        [Test]
        public void InputLimitNeverExportsAPartialRecordingOrStopsTheSimulation()
        {
            var p=Example();using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                for(int i=0;i<=SkillReplayJson.MaximumInputs;i++)s.Execute(new SkillSimulationCommand{Kind="cancel"});
                Assert.IsNotNull(s.Recording.Error);Assert.AreEqual(10000,s.Recording.Inputs);s.Advance(1);
                Assert.AreEqual(1,s.Snapshot().Step);Assert.Throws<ContentValidationException>(()=>s.CaptureReplay("basic-shot"));
            }
        }

        [Test]
        public void ComparisonToleratesFloatDriftButPreservesIdentityOrderAndSchema()
        {
            var expected=JObject.Parse("{\"x\":1.0,\"step\":1,\"ids\":[\"1\",\"2\"]}");var actual=(JObject)expected.DeepClone();actual["x"]=1.00005;
            Assert.IsTrue(SkillReplayComparer.Compare(expected,actual).Matches);
            actual["step"]=1.00001;Assert.IsFalse(SkillReplayComparer.Compare(expected,actual).Matches);
            actual=(JObject)expected.DeepClone();actual["ids"]=JArray.Parse("[\"2\",\"1\"]");Assert.IsFalse(SkillReplayComparer.Compare(expected,actual).Matches);
            actual=(JObject)expected.DeepClone();actual.Remove("x");Assert.AreEqual("$.x",SkillReplayComparer.Compare(expected,actual).Differences[0].Path);
            actual=(JObject)expected.DeepClone();actual["x"]=double.NaN;Assert.IsFalse(SkillReplayComparer.Compare(expected,actual).Matches);
        }
    }
}
