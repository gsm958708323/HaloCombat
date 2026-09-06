namespace Combat.Game
{
    public struct GameplayInputFrame
    {
        public float MoveX,
            MoveZ;
        public bool AttackPressed,
            Skill1Pressed,
            Skill2Pressed,
            Skill3Pressed,
            JumpPressed,
            DodgePressed,
            PausePressed,
            DebugToggleHitbox;
    }

    public interface IGameplayInputSource
    {
        GameplayInputFrame Sample();
    }

    public sealed class NullInputSource : IGameplayInputSource
    {
        public GameplayInputFrame Sample() => default(GameplayInputFrame);
    }
}
