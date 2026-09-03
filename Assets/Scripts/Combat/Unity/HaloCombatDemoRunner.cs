using System;
using Combat.Core;
using Combat.Demos;
using UnityEngine;

namespace Combat.Unity
{
    public enum HaloCombatDemoKind
    {
        TagInput = 0, Attribute = 1, Buff = 2, ActivityMotor = 3, ClipPayload = 4,
        MeleeDamage = 5, ProjectileAoe = 6, SeasonOne = 7, Knockdown = 8,
        DodgeHitstop = 9, AuraHoming = 10, BehaviorTree = 11, Perception = 12,
        EnemyAi = 13, Summon = 14, SeasonTwo = 15
    }

    public enum HaloCombatDemoCategoryFilter
    {
        All = 0, TagInput = 1, Attribute = 2, Buff = 3, ActivityMotor = 4,
        ClipPayload = 5, MeleeDamage = 6, ProjectileAoe = 7, SeasonOne = 8,
        Knockdown = 9, DodgeHitstop = 10, AuraHoming = 11, BehaviorTree = 12,
        Perception = 13, EnemyAi = 14, Summon = 15, SeasonTwo = 16
    }

    [DisallowMultipleComponent]
    public sealed class HaloCombatDemoRunner : MonoBehaviour
    {
        [SerializeField] HaloCombatDemoKind _demo;
        [SerializeField] bool _runOnStart = true;
        [SerializeField] HaloCombatDemoCategoryFilter _categoryFilter = HaloCombatDemoCategoryFilter.All;

        public HaloCombatDemoKind Demo => _demo;
        public HaloCombatDemoCategoryFilter CategoryFilter => _categoryFilter;
        public void Configure(HaloCombatDemoKind demo) => _demo = demo;
        public void SetCategoryFilter(HaloCombatDemoCategoryFilter filter) { _categoryFilter = filter; ApplyCategoryFilter(); }
        void OnEnable() => ApplyCategoryFilter();
        void OnValidate() => ApplyCategoryFilter();
        void Start() { ApplyCategoryFilter(); if (_runOnStart) RunDemo(); }

        [ContextMenu("Run Selected Demo")]
        public void RunDemo()
        {
            ApplyCategoryFilter();
            CombatLog.SetSink(new UnityLogSink(this));
            string category = DemoCategory(_demo);
            try
            {
                switch (_demo)
                {
                    case HaloCombatDemoKind.TagInput: TagInputDemo.Run(); break;
                    case HaloCombatDemoKind.Attribute: AttributeDemo.Run(); break;
                    case HaloCombatDemoKind.Buff: BuffDemo.Run(); break;
                    case HaloCombatDemoKind.ActivityMotor: ActivityMotorDemo.Run(); break;
                    case HaloCombatDemoKind.ClipPayload: ClipPayloadDemo.Run(); break;
                    case HaloCombatDemoKind.MeleeDamage: MeleeDamageDemo.Run(); break;
                    case HaloCombatDemoKind.ProjectileAoe: ProjectileAoeDemo.Run(); break;
                    case HaloCombatDemoKind.SeasonOne: SeasonOneDemo.Run(); break;
                    case HaloCombatDemoKind.Knockdown: KnockdownDemo.Run(); break;
                    case HaloCombatDemoKind.DodgeHitstop: DodgeHitstopDemo.Run(); break;
                    case HaloCombatDemoKind.AuraHoming: AuraHomingDemo.Run(); break;
                    case HaloCombatDemoKind.BehaviorTree: BehaviorTreeDemo.Run(); break;
                    case HaloCombatDemoKind.Perception: PerceptionDemo.Run(); break;
                    case HaloCombatDemoKind.EnemyAi: EnemyAiDemo.Run(); break;
                    case HaloCombatDemoKind.Summon: SummonDemo.Run(); break;
                    case HaloCombatDemoKind.SeasonTwo: SeasonTwoDemo.Run(); break;
                    default: throw new ArgumentOutOfRangeException();
                }
                CombatLog.Info(category, _demo + " PASSED");
            }
            catch (Exception exception)
            {
                CombatLog.Error(category, _demo + " FAILED", exception);
                throw;
            }
        }

        void ApplyCategoryFilter() => CombatLog.SetCategoryFilter(FilterCategory(_categoryFilter));
        static string FilterCategory(HaloCombatDemoCategoryFilter filter)
        {
            switch (filter)
            {
                case HaloCombatDemoCategoryFilter.TagInput: return CombatCategories.TagInput;
                case HaloCombatDemoCategoryFilter.Attribute: return CombatCategories.Attribute;
                case HaloCombatDemoCategoryFilter.Buff: return CombatCategories.Buff;
                case HaloCombatDemoCategoryFilter.ActivityMotor: return CombatCategories.ActivityMotor;
                case HaloCombatDemoCategoryFilter.ClipPayload: return CombatCategories.ClipPayload;
                case HaloCombatDemoCategoryFilter.MeleeDamage: return CombatCategories.MeleeDamage;
                case HaloCombatDemoCategoryFilter.ProjectileAoe: return CombatCategories.ProjectileAoe;
                case HaloCombatDemoCategoryFilter.SeasonOne: return CombatCategories.SeasonOne;
                case HaloCombatDemoCategoryFilter.Knockdown: return CombatCategories.Knockdown;
                case HaloCombatDemoCategoryFilter.DodgeHitstop: return CombatCategories.DodgeHitstop;
                case HaloCombatDemoCategoryFilter.AuraHoming: return CombatCategories.AuraHoming;
                case HaloCombatDemoCategoryFilter.BehaviorTree: return CombatCategories.BehaviorTree;
                case HaloCombatDemoCategoryFilter.Perception: return CombatCategories.Perception;
                case HaloCombatDemoCategoryFilter.EnemyAi: return CombatCategories.EnemyAi;
                case HaloCombatDemoCategoryFilter.Summon: return CombatCategories.Summon;
                case HaloCombatDemoCategoryFilter.SeasonTwo: return CombatCategories.SeasonTwo;
                default: return null;
            }
        }
        static string DemoCategory(HaloCombatDemoKind demo) => FilterCategory((HaloCombatDemoCategoryFilter)(int)demo);
    }
}
