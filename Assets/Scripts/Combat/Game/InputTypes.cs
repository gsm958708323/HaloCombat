namespace Combat.Game
{
    public struct GameplayInputFrame
    {
        public float MoveX,
            MoveZ;
        public bool AttackPressed,
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
