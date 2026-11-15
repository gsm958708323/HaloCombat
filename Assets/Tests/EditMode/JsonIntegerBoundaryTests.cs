using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class JsonIntegerBoundaryTests
    {
        static JObject Source()=>JObject.Parse(SkillProjectJson.Write(SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"))));
        [TestCase("identity")][TestCase("version")][TestCase("team")][TestCase("count")]
        public void FractionalDtoIntegersCannotBeRoundedBeforeDomainValidation(string field)
        {
            var token=Source();string path=field=="identity"?"skills[0].runtimeId":field=="version"?"formatVersion":field=="team"?"runtime.actors[0].team":"runtime.arena.maxEnemies";
            token.SelectToken(path).Replace(1.5);StringAssert.Contains(path,Assert.Throws<ContentValidationException>(()=>SkillProjectJson.Read(token.ToString())).Message);
        }
        [TestCase("1")][TestCase("1.0")][TestCase("invalid")]
        public void NumericStringsAreNotAnIntegerContract(string text)
        {var token=Source();token["runtime"]["actors"][0]["team"]=text;Assert.Throws<ContentValidationException>(()=>SkillProjectJson.Read(token.ToString()));}
        [Test]
        public void IntegralLegacyFloatIdsRemainExactAndExportAsNumbers()
        {
            var token=Source();int id=token["skills"][0].Value<int>("runtimeId");token["skills"][0]["runtimeId"]=(double)id;
            var project=SkillProjectJson.Read(token.ToString());Assert.AreEqual(id,project.Skills[0].RuntimeId);Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(project));
            Assert.AreEqual(JTokenType.Integer,JObject.Parse(SkillProjectJson.Write(project))["skills"][0]["runtimeId"].Type);
        }
        [TestCase(2147483648d)][TestCase(-2147483649d)][TestCase(double.PositiveInfinity)]
        public void OverflowAndNonfiniteIntegersFailAtTheProtocolBoundary(double value)
        {var token=Source();token["runtime"]["arena"]["seed"]=value;Assert.Throws<ContentValidationException>(()=>SkillProjectJson.Read(token.ToString()));}
    }
}
