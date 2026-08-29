using System;
using Combat.Core;
using Combat.Demos;
using UnityEngine;

namespace Combat.Unity
{
    public enum HaloCombatDemoKind
    {
        TagInput = 0,
        Attribute = 1,
        Buff = 2,
        ActivityMotor = 3,
        ClipPayload = 4,
        MeleeDamage = 5,
        ProjectileAoe = 6,
        SeasonOne = 7
    }

    public enum HaloCombatDemoCategoryFilter
    {
        All = 0,
        TagInput = 1,
        Attribute = 2,
        Buff = 3,
        ActivityMotor = 4,
        ClipPayload = 5,
        MeleeDamage = 6,
        ProjectileAoe = 7,
        SeasonOne = 8
    }

    [DisallowMultipleComponent]
    public sealed class HaloCombatDemoRunner : MonoBehaviour
    {
        [SerializeField] HaloCombatDemoKind _demo;
        [SerializeField] bool _runOnStart = true;
        [SerializeField] HaloCombatDemoCategoryFilter _categoryFilter = HaloCombatDemoCategoryFilter.All;

        public HaloCombatDemoKind Demo => _demo;
        public HaloCombatDemoCategoryFilter CategoryFilter => _categoryFilter;

        public void Configure(HaloCombatDemoKind demo)
        {
            _demo = demo;
        }

        public void SetCategoryFilter(HaloCombatDemoCategoryFilter filter)
        {
            _categoryFilter = filter;
            ApplyCategoryFilter();
        }

        void OnEnable()
        {
            ApplyCategoryFilter();
        }

        void OnValidate()
        {
            ApplyCategoryFilter();
        }

        void Start()
        {
            ApplyCategoryFilter();
            if (_runOnStart)
                RunDemo();
        }

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
                    case HaloCombatDemoKind.TagInput:
                        TagInputDemo.Run();
                        break;
                    case HaloCombatDemoKind.Attribute:
                        AttributeDemo.Run();
                        break;
                    case HaloCombatDemoKind.Buff:
                        BuffDemo.Run();
                        break;
                    case HaloCombatDemoKind.ActivityMotor:
                        ActivityMotorDemo.Run();
                        break;
                    case HaloCombatDemoKind.ClipPayload:
                        ClipPayloadDemo.Run();
                        break;
                    case HaloCombatDemoKind.MeleeDamage:
                        MeleeDamageDemo.Run();
                        break;
                    case HaloCombatDemoKind.ProjectileAoe:
                        ProjectileAoeDemo.Run();
                        break;
                    case HaloCombatDemoKind.SeasonOne:
                        SeasonOneDemo.Run();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                CombatLog.Info(category, "" + _demo + " PASSED");
            }
            catch (Exception exception)
            {
                CombatLog.Error(category, "" + _demo + " FAILED", exception);
                throw;
            }
        }

        void ApplyCategoryFilter()
        {
            CombatLog.SetCategoryFilter(FilterCategory(_categoryFilter));
        }

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
                default: return null;
            }
        }

        static string DemoCategory(HaloCombatDemoKind demo)
        {
            switch (demo)
            {
                case HaloCombatDemoKind.TagInput: return CombatCategories.TagInput;
                case HaloCombatDemoKind.Attribute: return CombatCategories.Attribute;
                case HaloCombatDemoKind.Buff: return CombatCategories.Buff;
                case HaloCombatDemoKind.ActivityMotor: return CombatCategories.ActivityMotor;
                case HaloCombatDemoKind.ClipPayload: return CombatCategories.ClipPayload;
                case HaloCombatDemoKind.MeleeDamage: return CombatCategories.MeleeDamage;
                case HaloCombatDemoKind.ProjectileAoe: return CombatCategories.ProjectileAoe;
                case HaloCombatDemoKind.SeasonOne: return CombatCategories.SeasonOne;
                default: return string.Empty;
            }
        }
    }
}
