using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Summon")]
    public sealed class SummonDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Lifetime;
        public float FollowRange = 2f;
        public float AcquireRadius = 8f;
        public BtNodeAsset Tree;
        public TreeRecipeKind Recipe = TreeRecipeKind.SummonMelee;

        public SummonDefinition Bake()
        {
            return new SummonDefinition
            {
                SpecId = SpecId,
                Lifetime = Lifetime,
                FollowRange = FollowRange,
                AcquireRadius = AcquireRadius,
                Tree = Tree != null ? Tree.Bake() : TreeRecipe.Build(Recipe)
            };
        }

        public void ClearCache() { }
        void OnValidate() => ClearCache();
    }
}
