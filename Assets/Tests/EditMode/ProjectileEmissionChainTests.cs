using System;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class ProjectileEmissionChainTests
    {
        [Test]
        public void SimultaneousSpreadHitsThreeDirectionsAndPaysForOneCast()
        {
            var p=Create("spread-shot");var node=Node(p,"projectile").Parameters;node["count"]=3;node["spread"]=60;
            Targets(p,(4,0),(3.4641016f,2),(3.4641016f,-2));
            using(var s=Run(p))
            {
                Assert.IsTrue(s.Cast("spread-shot",10,0).Accepted);s.Advance(50);var snapshot=s.Snapshot();
                Assert.AreEqual(3,snapshot.Statistics.Projectiles);Assert.AreEqual(3,snapshot.Statistics.Hits);Assert.AreEqual(240,snapshot.Statistics.Damage);
                Assert.AreEqual(1,snapshot.Statistics.Casts);Assert.AreEqual(90,snapshot.Energy);
                foreach(var target in snapshot.Actors.Where(a=>a.Kind=="target"))Assert.AreEqual(920,target.Hp);
                var damage=snapshot.Events.Where(e=>e.Kind=="damage").ToArray();
                Assert.AreEqual(1,damage.Select(e=>e.CastId).Distinct().Count());Assert.Greater(damage[0].CastId,0);
                Assert.IsTrue(damage.All(e=>e.Skill=="spread-shot"));
            }
        }

        [Test]
        public void FullCircleHasFourDistinctDirectionsAndDisablingMultiUsesOneRoot()
        {
            var p=Create("spread-shot");Targets(p);var parameters=Node(p,"projectile").Parameters;parameters["count"]=4;parameters["spread"]=360;
            using(var s=Run(p))
            {
                s.Cast("spread-shot",10,0);s.Advance(1);var actors=s.Snapshot().Actors.Where(a=>a.Kind=="projectile").ToArray();
                Assert.AreEqual(4,actors.Length);
                Assert.AreEqual(4,actors.Select(a=>(Math.Round(a.X,3),Math.Round(a.Z,3))).Distinct().Count());
                Assert.AreEqual(1,actors.Count(a=>a.X>.5f));Assert.AreEqual(1,actors.Count(a=>a.X<-.5f));
                Assert.AreEqual(1,actors.Count(a=>a.Z>.5f));Assert.AreEqual(1,actors.Count(a=>a.Z<-.5f));
            }
            parameters["multi"]=false;
            using(var s=Run(p)){s.Cast("spread-shot",10,0);s.Advance(1);Assert.AreEqual(1,s.Snapshot().Statistics.Projectiles);}
        }

        [Test]
        public void ChainRedirectsTheSameBodyAndHitsEachCompleteIdentityOnce()
        {
            var p=Create("chain-shot");Targets(p,(2,0),(3,2),(5,2),(5,-2));
            using(var s=Run(p))
            {
                s.Cast("chain-shot",10,0);s.Advance(150);var snapshot=s.Snapshot();
                Assert.AreEqual(320,snapshot.Statistics.Damage);Assert.AreEqual(4,snapshot.Statistics.Hits);
                Assert.AreEqual(1,snapshot.Statistics.Projectiles);Assert.AreEqual(3,snapshot.Statistics.Chains);
                CollectionAssert.AreEqual(new[]{"target-2","target-3","target-4"},snapshot.Events.Where(e=>e.Kind=="projectile-chain").Select(e=>e.Result));
                Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="projectile-end"));
            }
        }

        [Test]
        public void FinitePiercingBudgetResetsPerChainSegmentAndUnlimitedHitsNeverRepeat()
        {
            var p=Create("chain-shot");Targets(p,(2,0),(3,0),(4,0),(5,0),(6,0),(7,0));
            var parameters=Node(p,"projectile").Parameters;parameters["maxHits"]=2;parameters["chainCount"]=2;
            using(var s=Run(p))
            {s.Cast("chain-shot",10,0);s.Advance(100);Assert.AreEqual(480,s.Snapshot().Statistics.Damage);Assert.AreEqual(2,s.Snapshot().Statistics.Chains);}
            parameters["chain"]=false;parameters["maxHits"]=0;
            using(var s=Run(p))
            {s.Cast("chain-shot",10,0);s.Advance(150);Assert.AreEqual(480,s.Snapshot().Statistics.Damage);Assert.AreEqual(6,s.Snapshot().Statistics.Hits);}
        }

        [TestCase(false,"Range")]
        [TestCase(true,"Lifetime")]
        public void ChainNeverRefreshesTheParentDistanceOrLifetime(bool lifetime,string endReason)
        {
            var p=Create("chain-shot");Targets(p,(2,0),(4,0));
            var parameters=Node(p,"projectile").Parameters;parameters["range"]=lifetime?100:3;parameters["lifetime"]=lifetime?.25f:10;
            using(var s=Run(p))
            {
                s.Cast("chain-shot",10,0);s.Advance(100);var snapshot=s.Snapshot();
                Assert.AreEqual(80,snapshot.Statistics.Damage);Assert.AreEqual(1,snapshot.Statistics.Chains);
                Assert.AreEqual(endReason,snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);
            }
        }

        [Test]
        public void NoNextTargetEndsOnceAndWallsAreExcludedFromChainSearch()
        {
            var p=Create("chain-shot");Targets(p,(2,0),(4,0));
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=3,Z=0,Width=.1f,Depth=4}};
            using(var s=Run(p))
            {
                s.Cast("chain-shot",10,0);s.Advance(100);var snapshot=s.Snapshot();
                Assert.AreEqual(80,snapshot.Statistics.Damage);Assert.AreEqual(0,snapshot.Statistics.Chains);
                Assert.AreEqual("ChainExhausted",snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);
            }
        }

        [Test]
        public void RandomChainUsesTheWorldSeedAndVariesItsActualNextTarget()
        {
            var p=Create("chain-shot");Targets(p,(2,0),(4,2),(4,-2),(6,1));
            Node(p,"projectile").Parameters["chainMode"]="random";Node(p,"projectile").Parameters["chainCount"]=1;
            string sample;
            using(var s=Run(p)){s.Cast("chain-shot",10,0);s.Advance(100);sample=s.Snapshot().Events.Single(e=>e.Kind=="projectile-chain").Result;}
            using(var s=Run(p)){s.Cast("chain-shot",10,0);s.Advance(100);Assert.AreEqual(sample,s.Snapshot().Events.Single(e=>e.Kind=="projectile-chain").Result);}
            var samples=new System.Collections.Generic.HashSet<string>();
            for(int seed=1;seed<=8;seed++)
            {
                p.Preview.Seed=seed;
                using(var s=Run(p)){s.Cast("chain-shot",10,0);s.Advance(100);samples.Add(s.Snapshot().Events.Single(e=>e.Kind=="projectile-chain").Result);}
            }
            Assert.Greater(samples.Count,1);
        }

        [Test]
        public void HeldFanStopsNewEmissionAfterReleaseAndHitstopFreezesTheBodies()
        {
            var p=Create("fan-channel");Targets(p);Node(p,"cast").Parameters["energyPerSecond"]=0;
            using(var s=Run(p))
            {
                s.Cast("fan-channel",10,0);s.Advance(25);Assert.IsTrue(s.Snapshot().Channeling);
                Assert.Greater(s.Snapshot().Statistics.Projectiles,5);Assert.AreEqual(0,s.Snapshot().Statistics.Projectiles%5);
                s.Release();int count=s.Snapshot().Statistics.Projectiles;s.Advance(25);Assert.AreEqual(count,s.Snapshot().Statistics.Projectiles);
                var before=s.Snapshot().Actors.Where(a=>a.Kind=="projectile").Select(a=>(a.X,a.Z)).ToArray();
                s.RequestHitstop(10);s.Advance(10);CollectionAssert.AreEqual(before,s.Snapshot().Actors.Where(a=>a.Kind=="projectile").Select(a=>(a.X,a.Z)));
            }
        }

        [Test]
        public void WorldAliveQuotaAppliesAcrossSeveralIndependentEmissionFamilies()
        {
            var p=Create("spread-shot");Targets(p);
            var cast=Node(p,"cast").Parameters;cast["count"]=10;cast["interval"]=.001f;
            var projectile=Node(p,"projectile").Parameters;projectile["count"]=64;projectile["spread"]=0;
            using(var s=Run(p))
            {
                s.Cast("spread-shot",10,0);s.Advance(1);
                Assert.AreEqual(Combat.Core.ProjectileService.MaxAlive,s.Snapshot().Actors.Count(a=>a.Kind=="projectile"));
                Assert.AreEqual(512,s.Snapshot().Statistics.Projectiles);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="projectile-rejected"));
            }
        }
    }
}
