using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Selector")]
    public sealed class BtSelectorAsset : BtNodeAsset
    {
        public BtNodeAsset[] Children;
        public override BtNode Bake() => new BtSelector(BakeChildren(Children));

        static BtNode[] BakeChildren(BtNodeAsset[] children)
        {
            if (children == null || children.Length == 0)
                throw new InvalidOperationException("BtSelectorAsset requires at least one child.");
            var result = new BtNode[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null)
                    throw new InvalidOperationException("BtSelectorAsset contains an empty child at index " + i + ".");
                result[i] = children[i].Bake();
            }
            return result;
        }
    }
}
