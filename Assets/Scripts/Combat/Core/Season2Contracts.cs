namespace Combat.Core
{
    public static class Season2Contracts
    {
        public const bool AiMayStopDirector = false;
        public const bool HitstopAbortsBtActions = false;
        public const bool CloneTreePerActor = true;
        public const bool HitstopStartsNextFrame = true;
        public const bool SeasonTwoRequiresRangedEnemy = false;

        public static void EnsureAiMustNotStopDirector()
        {
            if (AiMayStopDirector)
                throw new System.InvalidOperationException("Contract broken: AI must not Director.Stop");
        }
    }

    public static class Season2Tokens
    {
        public static readonly InputToken Dodge = new InputToken("Dodge");
    }
}
