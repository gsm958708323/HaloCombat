namespace Combat.Core
{
    public abstract class ActorState
    {
        public abstract string Name { get; }
        public virtual bool CanEnterFrom(ActorStateId from) => true; // 默认可从任何状态进
        public virtual void OnEnter(StateEnterArgs args)
        {
            UnityEngine.Debug.LogFormat("Enter {0} from {1} Reason {2}", Name, args.From, args.Reason);
        }
        public abstract void Tick(float dt);
        public virtual void OnExit(StateExitReason reason)
        {
            UnityEngine.Debug.LogFormat("Exit {0} Reason {1}", Name, reason.Reason);
        }
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
