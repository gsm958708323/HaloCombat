using System.Collections.Generic;
using Combat.Core;

namespace Combat.Config
{
    internal static class SkillSceneValidation
    {
        public static void Validate(PreviewDocument preview)
        {
            if (preview == null) Fail("$.preview", "Scene is required.");
            if (!Finite(preview.Energy) || preview.Energy < 0) Fail("$.preview.energy", "Expected finite nonnegative capacity.");
            if (preview.InitialEnergy.HasValue && (!Finite(preview.InitialEnergy.Value) || preview.InitialEnergy < 0 || preview.InitialEnergy > preview.Energy))
                Fail("$.preview.initialEnergy", "Expected initial energy in [0, capacity].");
            if (!Finite(preview.Recovery) || preview.Recovery < 0) Fail("$.preview.recovery", "Expected finite nonnegative recovery.");
            if (preview.Targets == null || preview.Targets.Length > 128) Fail("$.preview.targets", "Expected up to 128 targets.");
            var collision=new CollisionWorld(Obstacles(preview));
            var ids = new HashSet<string>();
            Actor(preview.Caster, "$.preview.caster", ids);
            for (int i = 0; i < preview.Targets.Length; i++) Actor(preview.Targets[i], "$.preview.targets[" + i + "]", ids);
            CheckPlacement(preview.Caster,"$.preview.caster",collision);
            for(int i=0;i<preview.Targets.Length;i++)CheckPlacement(preview.Targets[i],"$.preview.targets["+i+"]",collision);
        }

        static void Actor(PreviewActorDocument actor, string path, HashSet<string> ids)
        {
            if (actor == null) Fail(path, "Actor is required.");
            if (string.IsNullOrWhiteSpace(actor.Id) || !ids.Add(actor.Id)) Fail(path + ".id", "Missing or duplicate identity.");
            if (!Finite(actor.X)) Fail(path + ".x", "Position must be finite.");
            if (!Finite(actor.Z)) Fail(path + ".z", "Position must be finite.");
            if (!Finite(actor.Hp) || actor.Hp <= 0) Fail(path + ".hp", "HP must be finite and positive.");
            if (!Finite(actor.Atk) || actor.Atk < 0) Fail(path + ".atk", "Attack must be finite and nonnegative.");
            if (!Finite(actor.Def) || actor.Def < 0) Fail(path + ".def", "Defense must be finite and nonnegative.");
            if(!Finite(actor.Shield)||actor.Shield<0||actor.Shield>10000000)Fail(path+".shield","Expected shield in [0,10000000].");
            if(!Finite(actor.DamageDealt)||actor.DamageDealt<0||actor.DamageDealt>100)Fail(path+".damageDealt","Expected multiplier in [0,100].");
            if(!Finite(actor.DamageTaken)||actor.DamageTaken<0||actor.DamageTaken>100)Fail(path+".damageTaken","Expected multiplier in [0,100].");
            if(!Finite(actor.CollisionRadius)||actor.CollisionRadius<0||actor.CollisionRadius>10)Fail(path+".collisionRadius","Expected movement radius in [0,10].");
        }

        static void CheckPlacement(PreviewActorDocument actor,string path,CollisionWorld collision)
        {if(!collision.IsFree(new SimVec3(actor.X,0,actor.Z),actor.CollisionRadius))Fail(path,"Actor overlaps an obstacle.");}
        public static ObstacleDefinition[] Obstacles(PreviewDocument scene)
        {
            if(scene.Obstacles==null||scene.Obstacles.Length>128)Fail("$.preview.obstacles","Expected up to 128 obstacles.");
            var ids=new HashSet<string>();var result=new ObstacleDefinition[scene.Obstacles.Length];
            for(int i=0;i<result.Length;i++)
            {
                var obstacle=scene.Obstacles[i];string path="$.preview.obstacles["+i+"]";
                if(obstacle==null)Fail(path,"Obstacle is required.");
                if(string.IsNullOrWhiteSpace(obstacle.Id)||!ids.Add(obstacle.Id))Fail(path+".id","Expected unique nonempty identity.");
                if(!Finite(obstacle.X))Fail(path+".x","Expected finite position.");
                if(!Finite(obstacle.Z))Fail(path+".z","Expected finite position.");
                if(!Finite(obstacle.Width)||obstacle.Width<=0||obstacle.Width>100)Fail(path+".width","Expected size in (0,100].");
                if(!Finite(obstacle.Depth)||obstacle.Depth<=0||obstacle.Depth>100)Fail(path+".depth","Expected size in (0,100].");
                result[i]=new ObstacleDefinition(obstacle.Id,obstacle.X,obstacle.Z,obstacle.Width,obstacle.Depth);
            }
            return result;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Fail(string path, string message) => throw new ContentValidationException(path, message);
    }
}
