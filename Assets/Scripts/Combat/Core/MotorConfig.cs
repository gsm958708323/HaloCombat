namespace Combat.Core
{
    public struct MotorConfig
    {
        public float Gravity;
        public float JumpSpeed;
        public float AirSteer;
        public float GroundY;
        public float StickDeadzone;

        public static MotorConfig SeasonOneDefaults()
        {
            return new MotorConfig
            {
                Gravity = -20f,
                JumpSpeed = 6f,
                AirSteer = 0.35f,
                GroundY = 0f,
                StickDeadzone = 0.25f
            };
        }
    }
}
