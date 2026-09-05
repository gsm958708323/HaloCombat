using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Sequence")]
    public sealed class BtSequenceAsset : BtNodeAsset
    {
        public BtNodeAsset[] Children;

        public override BtNode Bake()
        {
            if (Children == null || Children.Length == 0)
                throw new InvalidOperationException("BtSequenceAsset requires at least one child.");
            var result = new BtNode[Children.Length];
            for (int i = 0; i < Children.Length; i++)
            {
                if (Children[i] == null)
                    throw new InvalidOperationException("BtSequenceAsset contains an empty child at index " + i + ".");
                result[i] = Children[i].Bake();
            }
            return new BtSequence(result);
        }
    }
}
