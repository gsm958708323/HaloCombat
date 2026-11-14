using System;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class ProjectileSplitTests
    {
        [Test]
        public void ChildrenBeginNextTickAtTheImpactAndInheritCastIdentity()
        {
            var p=Create("split-shot");Targets(p,(2,0),(4,0));var split=Node(p,"split").Parameters;split["count"]=2;split["spread"]=0;
            using(var s=Run(p))
            {
                s.Cast("split-shot",10,0);s.Advance(8);Assert.AreEqual(1,s.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(80,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Statistics.Splits);
                s.Advance(1);var children=s.Snapshot().Actors.Where(a=>a.Kind=="projectile").ToArray();Assert.AreEqual(2,children.Length);
                foreach(var child in children)
                {
                    Assert.AreEqual(1,child.Generation);Assert.AreEqual(2.1f,child.X,.001f);Assert.AreEqual(.2f,child.Distance.Value,.001f);
                    Assert.AreEqual(8,child.Range);Assert.AreEqual("caster",child.Owner);Assert.AreEqual("split-shot",child.Skill);Assert.Greater(child.CastId,0);
                }
                s.Advance(60);Assert.AreEqual(160,s.Snapshot().Statistics.Damage);Assert.AreEqual(920,s.Snapshot().Actors.First(a=>a.Name=="target-1").Hp);
                Assert.AreEqual(920,s.Snapshot().Actors.First(a=>a.Name=="target-2").Hp);
                Assert.AreEqual(1,s.Snapshot().Events.Where(e=>e.Kind=="damage").Select(e=>e.CastId).Distinct().Count());
            }
        }

        [TestCase("inherit",80)]
        [TestCase("fixed",160)]
        public void ChildRangeCanUseTheActualRemainingPathOrAnExplicitNewDistance(string mode,int damage)
        {
            var p=Create("split-shot");Targets(p,(2,0),(4,0));Node(p,"projectile").Parameters["range"]=2.5f;
            var split=Node(p,"split").Parameters;split["count"]=2;split["spread"]=0;split["rangeMode"]=mode;split["range"]=8;
            using(var s=Run(p))
            {s.Cast("split-shot",10,0);s.Advance(100);Assert.AreEqual(damage,s.Snapshot().Statistics.Damage);Assert.AreEqual(3,s.Snapshot().Statistics.Projectiles);}
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitEffectOrderDeterminesWhetherSplitIsBeforeOrAfterDamage(bool before)
        {
            var p=Create("split-shot");Targets(p,(2,0));
            string projectile=Node(p,"projectile").Id,damage=Node(p,"damage").Id,split=Node(p,"split").Id;
            if(before)p.Skills[0].Edges=new[]{
                p.Skills[0].Edges.First(e=>e.TargetPort=="target"),p.Skills[0].Edges.First(e=>e.SourcePort=="cast"),
                new SkillEdgeDocument{Id="hit-split",Source=projectile,SourcePort="hit",Target=split,TargetPort="execute"},
                new SkillEdgeDocument{Id="split-damage",Source=split,SourcePort="next",Target=damage,TargetPort="execute"}};
            using(var s=Run(p))
            {
                s.Cast("split-shot",10,0);s.Advance(10);var events=s.Snapshot().Events;
                int splitSequence=events.Single(e=>e.Kind=="projectile-split").Sequence,damageSequence=events.Single(e=>e.Kind=="damage").Sequence;
                Assert.AreEqual(before,splitSequence<damageSequence);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);
            }
        }

        [TestCase(0,1,80,0)]
        [TestCase(1,3,160,1)]
        [TestCase(2,7,240,3)]
        public void GenerationLimitAndMultipliersApplyPerBranchWithoutHittingAncestors(int generations,int emitted,int damage,int splitEvents)
        {
            var p=Create("split-shot");Targets(p,(2,0),(4,0),(6,0));
            var split=Node(p,"split").Parameters;split["count"]=2;split["spread"]=0;split["generations"]=generations;
            using(var s=Run(p))
            {
                s.Cast("split-shot",10,0);s.Advance(150);var snapshot=s.Snapshot();
                Assert.AreEqual(emitted,snapshot.Statistics.Projectiles);Assert.AreEqual(damage,snapshot.Statistics.Damage);Assert.AreEqual(splitEvents,snapshot.Statistics.Splits);
                Assert.AreEqual(920,snapshot.Actors.First(a=>a.Name=="target-1").Hp);
            }
        }

        [Test]
        public void FanOutHasAFamilyQuotaAndSeparateCastsReceiveIndependentQuotas()
        {
            var p=Create("split-shot");Targets(p,Enumerable.Range(1,12).Select(i=>(i*2f,0f)).ToArray());
            var split=Node(p,"split").Parameters;split["count"]=32;split["spread"]=0;split["generations"]=8;
            foreach(int repetition in new[]{1,2})
                using(var s=Run(p))
                {
                    s.Cast("split-shot",30,0);s.Advance(200);
                    Assert.AreEqual(ProjectileFamily.MaxEmissions,s.Snapshot().Statistics.Projectiles);
                    Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="projectile-rejected"));
                    Assert.LessOrEqual(s.Snapshot().Actors.Count(a=>a.Kind=="projectile"),ProjectileService.MaxAlive);
                }
        }

        [Test]
        public void DeletingTheOwnerAfterImpactRejectsPendingChildrenAndStopsFutureDamage()
        {
            var p=Create("split-shot");Targets(p,(2,0),(4,0));
            using(var s=Run(p))
            {
                s.Cast("split-shot",10,0);s.Advance(8);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);
                s.Remove(s.Snapshot().Actors.First(a=>a.Kind=="caster").Id);s.Advance(80);
                Assert.AreEqual(1,s.Snapshot().Statistics.Projectiles);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);
                Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));
            }
        }

        [Test]
        public void OlderProjectilesKeepTheirCastWhenANewSkillStarts()
        {
            var p=Create("split-shot");Targets(p,(8,0),(12,0));Node(p,"projectile").Parameters["speed"]=2;
            var split=Node(p,"split").Parameters;split["count"]=1;split["spread"]=0;
            using(var s=Run(p))
            {
                s.Cast("split-shot",20,0);s.Advance(10);s.Cast("split-shot",20,0);s.Advance(10);
                var roots=s.Snapshot().Actors.Where(a=>a.Kind=="projectile").ToArray();Assert.AreEqual(2,roots.Length);
                Assert.AreEqual(2,roots.Select(a=>a.CastId).Distinct().Count());s.Advance(400);
                Assert.AreEqual(2,s.Snapshot().Events.Where(e=>e.Kind=="damage").Select(e=>e.CastId).Distinct().Count());
            }
        }
    }
}
