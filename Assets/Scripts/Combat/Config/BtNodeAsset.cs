using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    public abstract class BtNodeAsset : ScriptableObject
    {
        public abstract BtNode Bake();
    }
}
