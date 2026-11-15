using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillReplayTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static SkillSimulation Run(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static void Command(SkillSimulation s,string json)=>s.Execute(SkillSimulationCommands.Read(json));
        static void Matched(SkillReplayDocument document)
        {
            using(var replay=new SkillReplayRun(document))
            {
                Assert.IsTrue(replay.Comparison.Matches,string.Join("\n",replay.Comparison.Differences.Select(d=>d.Path+": "+d.Expected+" / "+d.Actual)));
                Assert.Greater(replay.Comparison.Values,50);
                Assert.AreEqual(document.EndStep,replay.Simulation.Snapshot().Step);
                Assert.AreEqual(document.Inputs.Length,replay.Simulation.Recording.Inputs);
            }
        }

        [Test]
        public void SameStepInputOrderIncludesGuardRejectionAndTheFinalStep()
        {
            using(var s=Run(Example()))
            {
                var command=new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0};
                Assert.IsTrue(((SkillCastResult)s.Execute(command)).Accepted);
                Assert.IsFalse(((SkillCastResult)s.Execute(command)).Accepted);
                s.Advance(60);Command(s,"{\"kind\":\"aim\",\"x\":4,\"z\":2}");
                var file=s.CaptureReplay("basic-shot");Assert.AreEqual(3,file.Inputs.Length);
                CollectionAssert.AreEqual(new[]{0,0,60},file.Inputs.Select(i=>i.AtStep));
                Assert.AreEqual(80,s.Snapshot().Statistics.Damage);Matched(file);
                var reversed=SkillReplayJson.Read(SkillReplayJson.Write(file));
                reversed.Inputs[0].Command.Z=10;using(var mismatch=new SkillReplayRun(reversed))Assert.IsFalse(mismatch.Comparison.Matches);
            }
        }

        [Test]
        public void ChannelMovementAimHitstopReleaseAndInterruptionUseTheRecordedClock()
        {
            using(var s=Run(Example()))
            {
                Command(s,"{\"kind\":\"cast\",\"skillId\":\"held-spray\",\"x\":0,\"z\":0}");
                Command(s,"{\"kind\":\"move\",\"x\":0,\"z\":1}");s.Advance(6);
                Command(s,"{\"kind\":\"hitstop\",\"frames\":3}");Command(s,"{\"kind\":\"aim\",\"x\":3,\"z\":1}");s.Advance(14);
                Command(s,"{\"kind\":\"release\",\"x\":2,\"z\":1}");Command(s,"{\"kind\":\"move\",\"x\":0,\"z\":0}");s.Advance(60);
                Command(s,"{\"kind\":\"cast\",\"skillId\":\"held-spray\",\"x\":0,\"z\":0}");s.Advance(4);
                Command(s,"{\"kind\":\"interrupt\"}");s.Advance(20);Command(s,"{\"kind\":\"death\"}");s.Advance(1);
                Matched(s.CaptureReplay("held-spray"));Assert.IsFalse(s.Snapshot().Channeling);
            }
        }

        [TestCase("charged-shot")] [TestCase("summon-gunners")] [TestCase("split-shot")]
        [TestCase("burn-shot")] [TestCase("barrel-chain")]
        public void SeededDerivedEntitiesEffectsAndCleanupReproduceTheWholeSnapshot(string skill)
        {
            var p=Example();p.Preview.Seed=472;
            using(var s=Run(p))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId=skill,X=0,Z=0});s.Advance(25);
                Command(s,"{\"kind\":\"release\"}");s.Advance(200);Matched(s.CaptureReplay(skill));
            }
        }

        [Test]
        public void PlaceRemoveAndDispelKeepTheirFullIdentityAndOrdering()
        {
            using(var s=Run(Example()))
            {
                Command(s,"{\"kind\":\"place\",\"actorId\":\"target-1\",\"x\":0,\"z\":0}");
                Command(s,"{\"kind\":\"cast\",\"skillId\":\"burn-shot\",\"x\":0,\"z\":0}");s.Advance(30);
                var owner=s.Snapshot().Actors.First(a=>a.Buffs.Length>0);
                s.Execute(new SkillSimulationCommand{Kind="remove-buff",ActorId=owner.Id,InstanceId=owner.Buffs[0].InstanceId});
                s.Advance(10);s.Execute(new SkillSimulationCommand{Kind="remove",EntityId=owner.Id});s.Advance(20);
                Matched(s.CaptureReplay("burn-shot"));
            }
        }

        [Test]
        public void RuntimeComboTokensAreRecordedThroughTheExistingInputBuffer()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            using(var s=Run(p))
            {
                Command(s,"{\"kind\":\"input\",\"input\":\"Fire1\",\"x\":4,\"z\":0}");s.Advance(20);
                Command(s,"{\"kind\":\"input\",\"input\":\"Fire1\",\"x\":4,\"z\":0}");s.Advance(60);
                Command(s,"{\"kind\":\"cancel\"}");Matched(s.CaptureReplay("g1"));
            }
        }

        [TestCase("random")] [TestCase("direction")]
        public void SeededTargetSelectionAndRandomDirectionAreReplayedAtTheirActualTriggerSteps(string mode)
        {
            var p=Example();p.Preview.Seed=967;
            p.Skills.Single(s=>s.Id=="burst-shot").Nodes.Single(n=>n.Kind=="target").Parameters["mode"]=mode;
            using(var s=Run(p))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="burst-shot",X=0,Z=0});s.Advance(80);
                Assert.AreEqual(3,s.Snapshot().Statistics.Projectiles);Matched(s.CaptureReplay("burst-shot"));
            }
        }

        [Test]
        public void ExportedConfigurationAndCommandsOwnIndependentSnapshots()
        {
            var p=Example();using(var s=Run(p))
            {
                var command=new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0};s.Execute(command);s.Advance(60);
                p.Preview.Seed=999;command.SkillId="missing";command.X=99;
                var file=s.CaptureReplay("basic-shot");Assert.AreEqual(1,file.Project.Preview.Seed);Assert.AreEqual("basic-shot",file.Inputs[0].Command.SkillId);
                file.Project.Preview.Seed=777;file.Inputs[0].Command.SkillId="missing";
                Matched(s.CaptureReplay("basic-shot"));
            }
        }

        [Test]
        public void BadReplayOrMissingViewsPreservesTheCurrentWorldAndValidReplayTransfersOwnership()
        {
            var p=Example();using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"basic-shot"))}});
                var old=session.Simulation;Command(old,"{\"kind\":\"cast\",\"skillId\":\"basic-shot\",\"x\":0,\"z\":0}");old.Advance(15);
                var file=old.CaptureReplay("basic-shot");string revision=session.Revision;
                file.ExpectedState["energy"]=-900;
                Assert.Throws<ContentValidationException>(()=>session.ReplaceReplay(SkillReplayJson.Write(file)));
                Assert.AreSame(old,session.Simulation);Assert.AreEqual(revision,session.Revision);
                old.Advance(45);Assert.AreEqual(80,old.Snapshot().Statistics.Damage);
                string valid=SkillReplayJson.Write(old.CaptureReplay("basic-shot"));
                session.ValidateViews=_=>throw new ContentValidationException("$.views","Missing resource");
                Assert.Throws<ContentValidationException>(()=>session.ReplaceReplay(valid));Assert.AreSame(old,session.Simulation);
                session.ValidateViews=null;Assert.IsTrue(session.ReplaceReplay(valid).Matches);Assert.AreNotSame(old,session.Simulation);
                Assert.Throws<ObjectDisposedException>(()=>old.Snapshot());Assert.AreEqual(60,session.Simulation.Snapshot().Step);
            }
        }
    }
}
