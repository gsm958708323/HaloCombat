using System;
using System.Reflection;
using Combat.Core;
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
#if UNITY_EDITOR
                var type = Type.GetType("Combat.Demos." + DemoTypeName(_demo) + ", Combat.Demos");
                if (type == null) throw new InvalidOperationException("Demo type not found: " + _demo);
                var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (method == null) throw new InvalidOperationException("Demo Run method not found: " + _demo);
                method.Invoke(null, null);
#else
                Debug.LogWarning("HaloCombat demos are editor-only.", this);
#endif
                CombatLog.Info(category, _demo + " PASSED");
            }
            catch (TargetInvocationException exception)
            {
                var inner = exception.InnerException ?? exception;
                CombatLog.Error(category, _demo + " FAILED", inner);
                throw inner;
            }
            catch (Exception exception)
            {
                CombatLog.Error(category, _demo + " FAILED", exception);
                throw;
            }
        }

        static string DemoTypeName(HaloCombatDemoKind demo)
        {
            switch (demo)
            {
                case HaloCombatDemoKind.TagInput: return "TagInputDemo";
                case HaloCombatDemoKind.Attribute: return "AttributeDemo";
                case HaloCombatDemoKind.Buff: return "BuffDemo";
                case HaloCombatDemoKind.ActivityMotor: return "ActivityMotorDemo";
                case HaloCombatDemoKind.ClipPayload: return "ClipPayloadDemo";
                case HaloCombatDemoKind.MeleeDamage: return "MeleeDamageDemo";
                case HaloCombatDemoKind.ProjectileAoe: return "ProjectileAoeDemo";
                case HaloCombatDemoKind.SeasonOne: return "SeasonOneDemo";
                case HaloCombatDemoKind.Knockdown: return "KnockdownDemo";
                case HaloCombatDemoKind.DodgeHitstop: return "DodgeHitstopDemo";
                case HaloCombatDemoKind.AuraHoming: return "AuraHomingDemo";
                case HaloCombatDemoKind.BehaviorTree: return "BehaviorTreeDemo";
                case HaloCombatDemoKind.Perception: return "PerceptionDemo";
                case HaloCombatDemoKind.EnemyAi: return "EnemyAiDemo";
                case HaloCombatDemoKind.Summon: return "SummonDemo";
                case HaloCombatDemoKind.SeasonTwo: return "SeasonTwoDemo";
                default: throw new ArgumentOutOfRangeException(nameof(demo));
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
