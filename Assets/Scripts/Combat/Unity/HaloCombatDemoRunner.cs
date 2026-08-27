using System;
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

    [DisallowMultipleComponent]
    public sealed class HaloCombatDemoRunner : MonoBehaviour
    {
        [SerializeField] HaloCombatDemoKind _demo;
        [SerializeField] bool _runOnStart = true;

        public HaloCombatDemoKind Demo => _demo;

        public void Configure(HaloCombatDemoKind demo)
        {
            _demo = demo;
        }

        void Start()
        {
            if (_runOnStart)
                RunDemo();
        }

        [ContextMenu("Run Selected Demo")]
        public void RunDemo()
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

            Debug.Log("[HaloCombat] " + _demo + " PASSED", this);
        }
    }
}
