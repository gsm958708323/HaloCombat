namespace Combat.Core
{
    public abstract class ActorState
    {
        public abstract string Name { get; }
        public virtual bool CanEnterFrom(ActorStateId from) => true; // 默认可从任何状态进
        public abstract void OnEnter(StateEnterArgs args);
        public abstract void Tick(float dt);
        public abstract void OnExit(StateExitReason reason);
    }

    public struct StateEnterArgs
    {
        public ActorStateId From;
        public string Reason; // "Input", "Hit", "Effect", "Despawn" 等

        public StateEnterArgs(ActorStateId from, string reason)
        {
            From = from;
            Reason = reason ?? string.Empty;
        }
    }

    public struct StateExitReason
    {
        public string Reason; // "Hit", "Cancel", "Despawn" 等
    }
}
