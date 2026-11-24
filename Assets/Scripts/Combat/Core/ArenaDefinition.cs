using System;

namespace Combat.Core
{
    public sealed class ArenaDefinition
    {
        public string Player,Enemy,TrainingTarget;
        public int Seed,MaxEnemies;
        public float SpawnPeriod,CleanupDelay,HalfWidth,HalfDepth,SpawnDistance;
        public SimVec3 PlayerPosition;
        public float PlayerYaw;
        public ObstacleDefinition[] Obstacles=Array.Empty<ObstacleDefinition>();
        public ArenaBinding[] Bindings=Array.Empty<ArenaBinding>();
        public float CameraDistance,CameraHeight,CameraFov,CameraFocusHeight;
        public float CameraDownedFocusHeight=.55f,CameraSpringHz=8;
        public bool CameraShakeEnabled=true;
    }
    public readonly struct ArenaBinding
    {
        public readonly string Action;
        public readonly InputToken Input;
        public ArenaBinding(string action,InputToken input){Action=action;Input=input;}
    }
    public struct ArenaInputFrame
    {
        public float MoveX,MoveZ;
        public SkillAim Aim;
        public bool AimValid,PrimaryHeld;
        public string[] Pressed;
    }
    public sealed class PlayerAimComp : Comp
    {
        public SkillAim Aim;
        public bool Valid,PrimaryHeld;
        public int PrimaryCastId;
        public InputToken PrimaryToken;
        protected override void OnDetach(){Aim=default;Valid=false;PrimaryHeld=false;PrimaryCastId=0;PrimaryToken=default;}
    }
}
