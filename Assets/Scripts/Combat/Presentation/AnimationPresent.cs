using Combat.Core;
using UnityEngine;

namespace Combat.Presentation
{
    public struct AnimFlags
    {
        public float Speed;
        public bool Grounded,
            InAir,
            Attack,
            Hit,
            Downed,
            Dead,
            IFrame;
        public int SkillId;
        public float SkillTime,
            SkillDuration;
        public bool Hitstop;
        public SkillAnimationMode AnimationMode;

        public static AnimFlags Capture(Actor a, bool hitstop)
        {
            var f = default(AnimFlags);
            if (a == null)
                return f;
            f.Hitstop = hitstop;
            if (a.TryGetComp<TagComp>(out var tags))
            {
                f.Grounded = tags.Has(CommonTags.Grounded);
                f.InAir = tags.Has(CommonTags.Airborne);
                f.Downed = tags.Has(CommonTags.Downed);
                f.Dead = tags.Has(CommonTags.Dead);
                f.IFrame = tags.Has(CommonTags.Invincible);
            }
            if (a.TryGetComp<StateMachineComp>(out var sm))
            {
                f.Attack = sm.Current == ActivityId.Attack;
                f.Hit = sm.Current == ActivityId.Hit;
                f.Dead |= sm.Current == ActivityId.Dead;
                f.Downed |= sm.Current == ActivityId.Knockdown;
            }
            if (a.TryGetComp<SkillDirectorComp>(out var dir) && dir.IsPlaying)
            {
                f.SkillId = dir.CurrentSkill.Value;
                f.SkillTime = dir.CurrentTime;
                f.SkillDuration = dir.CurrentDuration;
                f.AnimationMode = dir.CurrentAnimationMode;
            }
            if (a.TryGetComp<LocomotionComp>(out var loco))
            {
                float m = LocomotionComp.StickMag(loco.MoveIntent);
                f.Speed = m > 1f ? 1f : m;
            }
            return f;
        }
    }

    public sealed class AnimationPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public AnimFlags Flags { get; private set; }
        public AnimFlags Applied { get; private set; }

        public override void SyncLogic(CombatWorld world)
        {
            if (Self.TryLogic(world, out var a))
                Flags = AnimFlags.Capture(a, world.InHitstop);
        }

        public override void LateTick(float dt)
        {
            Applied = Flags;
            if (!Self.TryGet<ActorViewPresent>(out var view))
                return;
            var animator = view.Animator;
            if (animator == null)
                return;

            SetBool(animator, "Grounded", Flags.Grounded);
            SetBool(animator, "InAir", Flags.InAir);
            SetBool(animator, "Attack", Flags.Attack);
            SetBool(animator, "Hit", Flags.Hit);
            SetBool(animator, "Downed", Flags.Downed);
            SetBool(animator, "Dead", Flags.Dead);
            SetBool(animator, "IFrame", Flags.IFrame);
            SetBool(animator, "Hitstop", Flags.Hitstop);
            SetFloat(animator, "Speed", Flags.Speed);
            SetInteger(animator, "SkillId", Flags.SkillId);
            SetInteger(animator, "SkillMode", (int)Flags.AnimationMode);
            if (Flags.Attack && Flags.SkillId != 0 && Flags.SkillDuration > 0f)
            {
                var state = SkillState(Flags.AnimationMode);
                var progress = Mathf.Clamp01(Flags.SkillTime / Flags.SkillDuration);
                if (animator.HasState(0, state))
                {
                    animator.speed = 0f;
                    animator.Play(state, 0, progress);
                    return;
                }
            }
            animator.speed = Flags.Hitstop ? 0f : 1f;
        }

        protected override void OnDetach()
        {
            Flags = default;
            Applied = default;
        }

        static void SetBool(Animator animator, string name, bool value)
        {
            if (animator.HasParameter(name, AnimatorControllerParameterType.Bool))
                animator.SetBool(name, value);
        }

        static void SetFloat(Animator animator, string name, float value)
        {
            if (animator.HasParameter(name, AnimatorControllerParameterType.Float))
                animator.SetFloat(name, value);
        }

        static void SetInteger(Animator animator, string name, int value)
        {
            if (animator.HasParameter(name, AnimatorControllerParameterType.Int))
                animator.SetInteger(name, value);
        }

        static int SkillState(SkillAnimationMode mode)
        {
            switch (mode)
            {
                case SkillAnimationMode.AirAttack:
                    return Animator.StringToHash("AirAttack");
                case SkillAnimationMode.Dash:
                    return Animator.StringToHash("Dash");
                case SkillAnimationMode.Slide:
                    return Animator.StringToHash("Slide");
                default:
                    return Animator.StringToHash("Attack");
            }
        }
    }

    static class AnimatorExtensions
    {
        public static bool HasParameter(
            this Animator animator,
            string name,
            AnimatorControllerParameterType type
        )
        {
            if (animator == null || animator.parameters == null)
                return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].name == name && parameters[i].type == type)
                    return true;
            return false;
        }
    }
}
