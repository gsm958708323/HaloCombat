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
    public sealed class ComboAuthoringTests
    {
        static SkillProjectDocument Project()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            p.Preview.UnlimitedAmmo=false;p.Preview.Recovery=0;p.Preview.InitialEnergy=100;p.Preview.Energy=100;
            p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            p.Preview.InputBufferSeconds=.8f;
            // A non-Arena preview uses its own input window.
            p.Runtime.Arena=null;return p;
        }
        [Test]
        public void InputDecisionCapturesSelectedRuleAndActualSuccessfulSkill()
        {
            var p=Project();using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p),false))
            {
                sim.Input("ConfirmAttack",4,0);sim.Advance(1);var snapshot=sim.Snapshot();
                var d=snapshot.Actors.Single(a=>a.Kind=="caster").ComboDecision;
                Assert.AreEqual("cast",d.Phase);Assert.AreEqual("confirmed-root",d.Rule);Assert.AreEqual("g1",d.ActualSkill);
                Assert.IsNull(d.FromSkill);Assert.Greater(d.InputSequence,0);
                Assert.IsTrue(snapshot.ComboDecisions.Any(e=>e.ComboDecision?.Phase=="cast"));
            }
        }
        [Test]
        public void WaitingAndMissingConfirmationExplainRealConditionsWithoutRepeatedFrameRecords()
        {
            var p=Project();using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p),false))
            {
                sim.Input("ConfirmAttack",4,0);sim.Advance(1);sim.Input("ConfirmAttack",4,0);sim.Advance(1);
                Assert.AreEqual("waiting",sim.Snapshot().Actors.Single(a=>a.Kind=="caster").ComboDecision.Phase);
                sim.Advance(9);var snapshot=sim.Snapshot();var d=snapshot.Actors.Single(a=>a.Kind=="caster").ComboDecision;
                Assert.AreEqual("unmatched",d.Phase);Assert.IsTrue(d.WindowOpen);
                CollectionAssert.Contains(d.Candidates.Single(c=>c.Rule=="confirmed-g1-g2").MissingTags,1017);
                int count=snapshot.ComboDecisions.Length;sim.Advance(2);
                Assert.AreEqual(count,sim.Snapshot().ComboDecisions.Length,"Identical waiting presses are not recorded every frame.");
            }
        }
        [Test]
        public void ResourceFailureIsDistinctFromRuleSelectionAndKeepsBufferedInput()
        {
            var p=Project();p.Preview.InitialEnergy=0;
            p.Skills.Single(s=>s.Id=="g1").Nodes.Single(n=>n.Kind=="cast").Parameters["cost"]=20;
            using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p),false))
            {
                sim.Input("ConfirmAttack",4,0);sim.Advance(3);var snapshot=sim.Snapshot();var actor=snapshot.Actors.Single(a=>a.Kind=="caster");
                Assert.AreEqual("rejected",actor.ComboDecision.Phase);Assert.AreEqual("Insufficient energy.",actor.ComboDecision.Reason);
                Assert.AreEqual("confirmed-root",actor.ComboDecision.Rule);Assert.IsNull(actor.ComboDecision.ActualSkill);
                Assert.AreEqual("ConfirmAttack",actor.BufferedInput);Assert.AreEqual(0,snapshot.Statistics.Casts);
                Assert.AreEqual(2,snapshot.ComboDecisions.Length);
            }
        }
        [Test]
        public void DiagnosticReportsActualAmmoFallbackInsteadOfDrawingARequestedSkillAsExecuted()
        {
            var p=Project();p.Preview.Ammo=6;p.Preview.InitialAmmo=0;
            using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p),false))
            {
                sim.Input("ArenaPrimary",4,0);sim.Advance(1);var d=sim.Snapshot().Actors.Single(a=>a.Kind=="caster").ComboDecision;
                Assert.AreEqual("cast",d.Phase);Assert.AreEqual("arena-ammo-shot",d.ToSkill);Assert.AreEqual("arena-reload-ammo",d.ActualSkill);
            }
        }
        [Test]
        public void BufferedPressCanBeReevaluatedOnALaterStepAfterConfirmationWasConsumed()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            p.Runtime.Actors.Single(a=>a.Id=="arena-player").Recovery=0;
            p.Skills.Single(s=>s.Id=="g2").Nodes.Single(n=>n.Kind=="cast").Parameters["cost"]=20;
            p.Runtime.Combos=p.Runtime.Combos.Append(new RuntimeComboDocument{Id="after-failed-confirmation",Purpose="transition",PreSkills=new[]{"g1"},
                Input="ConfirmAttack",Priority=1,ToSkill="arena-returning-shot"}).ToArray();
            using(var session=new ArenaProject(p).Create(true))
            {
                session.Start();session.Advance();session.World.TryGetActor(session.PlayerId,out var actor);
                var combo=actor.GetComp<ComboComp>();combo.DiagnosticsEnabled=true;actor.GetComp<EnergyComp>().TrySpend(100);
                Assert.IsTrue(actor.GetComp<SkillCastComp>().TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)));
                var director=actor.GetComp<SkillDirectorComp>();var target=session.World.RegistryActive().First(a=>a.Id!=actor.Id);
                session.World.Deliver(new IEffect[]{new DamageEffect{Coeff=0,Flat=0}},actor,target,0,origin:director.CurrentOrigin);
                using(actor.GetComp<TagComp>().Acquire(CommonTags.Cancel))
                {
                    actor.GetComp<InputBufferComp>().Push(new InputToken("ConfirmAttack"));session.Advance();
                    var failed=combo.LastDecision;Assert.AreEqual("rejected",failed.Phase);Assert.AreEqual("confirmed-g1-g2",failed.Rule);
                    session.Advance();var next=combo.LastDecision;
                    Assert.AreEqual(failed.InputSequence,next.InputSequence);Assert.AreEqual("cast",next.Phase);
                    Assert.AreEqual("after-failed-confirmation",next.Rule,"A later fixed step evaluates the still-buffered press against changed state.");
                }
            }
        }
        [Test]
        public void AuthorViewsRoundTripAndRuntimeExportRetainsPurposeWithoutViewState()
        {
            var p=Project();p.Runtime.Combos.Single(c=>c.Id=="confirmed-g1-g2").Purpose="transition";
            p.Editor=new SkillEditorDocument{ComboViews=new[]{new ComboViewDocument{Id="test-view",Name="命中派生",RootSkill="g1",RuleIds=new[]{"confirmed-g1-g2"}}}};
            string json=SkillJsonTransfer.Export(p,"project").Json;var imported=SkillJsonTransfer.Import(json).Project;
            Assert.AreEqual("test-view",imported.Editor.ComboViews.Single().Id);
            var runtime=JObject.Parse(SkillJsonTransfer.Export(p,"runtime").Json);
            Assert.IsNull(runtime["editor"]);Assert.AreEqual("transition",(string)runtime["runtime"]["combos"].Single(c=>(string)c["id"]=="confirmed-g1-g2")["purpose"]);
        }
        [Test]
        public void InvalidPurposeAndDanglingAuthorViewAreRejectedAtTheProtocolBoundary()
        {
            var p=Project();p.Runtime.Combos.Single(c=>c.Id=="confirmed-root").Purpose="cancel";
            Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));
            p.Runtime.Combos.Single(c=>c.Id=="confirmed-root").Purpose="entry";
            p.Editor=new SkillEditorDocument{ComboViews=new[]{new ComboViewDocument{Id="bad",Name="坏引用",RootSkill="g1",RuleIds=new[]{"missing"}}}};
            Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));
        }
        [Test]
        public void DiagnosticChannelPreservesGameplayEventSequencesAndRecordedOutcomeShape()
        {
            var p=Project();using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                sim.Execute(new SkillSimulationCommand{Kind="input",Input="ConfirmAttack",X=4,Z=0});sim.Advance(12);
                var snapshot=sim.Snapshot();Assert.Greater(snapshot.ComboDecisions.Length,0);
                Assert.IsFalse(snapshot.Events.Any(e=>e.Kind=="combo-decision"));
                var record=sim.CaptureReplay("g1");Assert.IsNull(record.ExpectedState["comboDecisions"]);
                Assert.IsTrue(((JArray)record.ExpectedState["actors"]).All(a=>a["comboDecision"]==null&&a["comboInputSequence"]==null));
                using(var replay=new SkillReplayRun(record))Assert.IsTrue(replay.Comparison.Matches);
            }
        }
        [Test]
        public void RepeatedHeldPrimaryActionsEachPublishTheirOwnSuccessfulDecision()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            using(var session=new ArenaProject(p).Create(true))
            {
                session.Start();session.Advance();session.World.TryGetActor(session.PlayerId,out var actor);
                var combo=actor.GetComp<ComboComp>();combo.DiagnosticsEnabled=true;
                var aim=actor.GetComp<PlayerAimComp>();aim.PrimaryHeld=true;aim.PrimaryToken=new InputToken("ArenaPrimary");
                int successes=0;session.World.Events.Subscribe<EvComboDecision>(e=>{if(e.Source==actor.Id&&e.Decision.Phase=="cast")successes++;});
                session.Advance(100);Assert.Greater(successes,2,"Each successful held repetition remains visible even with no new buffered press.");
            }
        }
    }
}
