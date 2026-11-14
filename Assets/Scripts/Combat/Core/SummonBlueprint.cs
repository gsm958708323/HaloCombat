using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class SummonBlueprint
    {
        public string Id,Name,SkillKey;
        public SkillNodeId Skill;
        public float Hp=100,Attack=80,Defense,MoveSpeed=3,CollisionRadius=.4f;
        public float Energy=100,InitialEnergy=100,Recovery=10;
        public int Ammo,InitialAmmo;
        public float AcquireRadius=20,AttackRange=12,FollowRange=2,HoldDuration=.75f;
    }

    public sealed class SummonBlueprintCatalog
    {
        readonly Dictionary<string,SummonBlueprint> _items=new Dictionary<string,SummonBlueprint>(StringComparer.Ordinal);
        public void Register(SummonBlueprint blueprint)
        {
            if(blueprint==null||string.IsNullOrWhiteSpace(blueprint.Id)||_items.ContainsKey(blueprint.Id))throw new ArgumentException("Invalid or duplicate summon blueprint.");
            _items.Add(blueprint.Id,blueprint);
        }
        public bool TryGet(string id,out SummonBlueprint blueprint)=>_items.TryGetValue(id??"",out blueprint);
    }
}
