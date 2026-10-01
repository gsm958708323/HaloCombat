using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Motor")]
    public sealed class CharacterMotorAsset : ScriptableObject
    {
        public float Gravity = -20f;
        public float JumpSpeed = 6f;
        public float AirSteer = 0.35f;
        public float GroundY;
        public float StickDeadzone = 0.25f;

        public MotorConfig Bake() => new MotorConfig
        {
            Gravity = Gravity,
            JumpSpeed = JumpSpeed,
            AirSteer = AirSteer,
            GroundY = GroundY,
            StickDeadzone = StickDeadzone
        };
    }
}
