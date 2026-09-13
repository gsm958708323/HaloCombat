namespace Combat.Game
{
    public sealed class PauseStack
    {
        public int Depth { get; private set; }
        public bool Paused => Depth > 0;

        public void Push() => Depth++;

        public void Pop()
        {
            if (Depth > 0)
                Depth--;
        }

        public void Clear() => Depth = 0;
    }
}
