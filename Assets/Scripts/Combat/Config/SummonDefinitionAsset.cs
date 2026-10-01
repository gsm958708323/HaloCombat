using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Summon Definition")]
    public sealed class SummonDefinitionAsset : ScriptableObject
    {
        public int SpecId;

        public int RequireId()
        {
            if (SpecId == 0) throw new InvalidOperationException(name + ": SpecId is required.");
            return SpecId;
        }
    }
}
