using System;
using Combat.Core;

namespace Combat.Demos
{
    /// <summary>
    /// Demo 层的轻量教学记录器。它只读取 CombatWorld 的公开状态，不参与战斗结算。
    /// </summary>
    internal sealed class DemoTrace
    {
        readonly string _caseId;
        readonly string _category;
        readonly CombatWorld _world;
        readonly Action<float> _tick;
        int _step;
        bool _completed;

        public string CaseId => _caseId;
        public string Category => _category;
        public int StepCount => _step;

        public DemoTrace(string caseId, string category, CombatWorld world, Action<float> tick = null)
        {
            if (string.IsNullOrWhiteSpace(caseId))
                throw new ArgumentException("Demo 名称不能为空。", nameof(caseId));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Demo 分类不能为空。", nameof(category));

            _caseId = caseId;
            _category = category;
            _world = world;
            _tick = tick ?? (dt => _world?.Tick(dt));
        }

        public void Step(string title, Func<string> details = null)
        {
            BeginStep(title, "进行中", details);
        }

        public void Check(
            string title,
            bool condition,
            string expected,
            string actual,
            Func<string> details = null)
        {
            string extra = details?.Invoke();
            string status = condition ? "通过" : "失败";
            BeginStep(title, status, () =>
            {
                string text = $"期望={expected ?? string.Empty} 实际={actual ?? string.Empty}";
                return string.IsNullOrEmpty(extra) ? text : $"{text} {extra}";
            });

            if (!condition)
                throw new InvalidOperationException(
                    $"Demo {_caseId} 步骤 {_step} 失败：{title}；期望={expected ?? string.Empty}；实际={actual ?? string.Empty}；帧={Frame()}；时间={Time()}；状态={(extra ?? "无")}");
        }

        /// <summary>
        /// 明确要观察一段固定时间后的状态
        /// </summary>
        public void AdvanceFor(string title, float dt, int count, Func<string> details = null)
        {
            if (count < 0) count = 0;
            if (dt < 0f) dt = 0f;
            for (int i = 0; i < count; i++)
                _tick(dt);

            BeginStep(title, "完成", () =>
            {
                string text = $"推进={count} 帧 dt={dt:F3}";
                string extra = details?.Invoke();
                return string.IsNullOrEmpty(extra) ? text : $"{text} {extra}";
            });
        }

        /// <summary>
        /// 明确要等待某个事件/状态出现，但不能无限等
        /// </summary>
        public bool AdvanceUntil(
            string title,
            Func<bool> condition,
            float dt,
            int maxSteps,
            Func<string> details = null)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (maxSteps < 0) maxSteps = 0;
            if (dt < 0f) dt = 0f;

            int steps = 0;
            while (!condition() && steps < maxSteps)
            {
                _tick(dt);
                steps++;
            }

            bool reached = condition();
            string actual = details?.Invoke();
            BeginStep(title, reached ? "通过" : "超时", () =>
            {
                string text = $"推进={steps}/{maxSteps} 帧 dt={dt.ToString("F3")}";
                return string.IsNullOrEmpty(actual) ? text : $"{text} {actual}";
            });

            if (!reached)
                throw new InvalidOperationException(
                    $"Demo {_caseId} 步骤 {_step} 超时：{title}；最大帧数={maxSteps}；帧={Frame()}；时间={Time()}；状态={(actual ?? "无")}");
            return true;
        }

        public void Complete(string summary = null)
        {
            if (_completed) return;
            _completed = true;
            string text = $"[DemoComplete][{_caseId}] 通过 steps={_step}";
            if (!string.IsNullOrEmpty(summary)) text = $"{text} {summary}";
            CombatLog.Info(_category, $"{text} status=PASSED");
        }

        void BeginStep(string title, string status, Func<string> details)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("步骤标题不能为空。", nameof(title));

            _step++;
            string text = $"[DemoStep][{_caseId}][step={_step}] {title} 状态={status} frame={Frame()} time={Time()}";
            string extra = details?.Invoke();
            if (!string.IsNullOrEmpty(extra)) text = $"{text} {extra}";
            CombatLog.Info(_category, text);
        }

        string Frame() => _world?.Time != null ? _world.Time.Frame.ToString() : "-";
        string Time() => _world?.Time != null ? _world.Time.Time.ToString("F3") : "-";

        /// <summary>
        /// 生成适合放进步骤详情的 Actor 快照，避免每个 Demo 重复拼接状态字段。
        /// </summary>
        public static string Snapshot(Actor actor)
        {
            if (actor == null) return "actor=null";

            string state = "无";
            if (actor.TryGetComp<StateMachineComp>(out var fsm))
                state = fsm.Current.ToString();

            string hp = "-";
            if (actor.TryGetComp<AttributeSet>(out var attr))
                hp = attr.GetBase(AttrId.Hp).ToString("F1");

            string pos = "-";
            if (actor.TryGetComp<TransformComp>(out var tf))
                pos = $"({tf.Position.X.ToString("F2")},{tf.Position.Y.ToString("F2")},{tf.Position.Z.ToString("F2")})";

            string tags = "";
            if (actor.TryGetComp<TagComp>(out var tagComp))
            {
                tags = $" grounded={tagComp.Has(CommonTags.Grounded)} cancel={tagComp.Has(CommonTags.Cancel)} iframe={tagComp.Has(CommonTags.Invincible)} downed={tagComp.Has(CommonTags.Downed)}";
            }

            return $"actor={actor.Id} active={actor.IsActive} state={state} hp={hp} pos={pos}{tags}";
        }
    }
}
