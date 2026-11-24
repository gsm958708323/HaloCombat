import {useEffect, useMemo, useState} from 'react';
import {Pause, Play, RotateCcw, SkipForward, Zap} from 'lucide-react';
import type {ComboDecision, Project} from './model';
import {inputColor, inputShortLabel, knownInputTokens} from './authoring/comboModel';
import type {usePreview} from './usePreview';

function describeDecision(decision: ComboDecision | null, buffered: string | null): string {
  if (!decision) return buffered ? `缓冲中 · ${buffered}` : '';
  switch (decision.phase) {
    case 'waiting':
      return buffered ? '接续窗口未开，输入缓冲等待中' : '接续窗口未开，缓冲已过期';
    case 'blocked':
      return '当前角色状态禁止接招';
    case 'unmatched': {
      const reasons = [...new Set(decision.candidates.flatMap(c => c.failedConditions ?? []))];
      if (reasons.length) return `未满足条件 · ${reasons.slice(0, 2).join(' / ')}`;
      return '没有满足条件的连招规则';
    }
    case 'rejected':
      return decision.reason ? `施放被拒 · ${decision.reason}` : '施放被拒';
    default:
      return '';
  }
}

export function ComboRunFeedback({project, preview}: {project: Project; preview: ReturnType<typeof usePreview>}) {
  const tokens = useMemo(() => knownInputTokens(project), [project]);
  const [activeToken, setActiveToken] = useState('');
  const selectedToken = tokens.includes(activeToken) ? activeToken : tokens[0] ?? '';

  const caster = preview.state?.actors.find(actor => actor.kind === 'caster');
  const currentSkill = caster?.currentSkill ? project.skills.find(s => s.id === caster.currentSkill)?.name ?? caster.currentSkill : null;
  const decision = caster?.comboDecision?.inputSequence === caster?.comboInputSequence ? caster?.comboDecision ?? null : null;
  const feedback = describeDecision(decision, caster?.bufferedInput ?? null);

  // Hotkeys 1..9 trigger logical inputs directly when not typing in a form field.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.repeat || event.ctrlKey || event.metaKey || event.altKey) return;
      if ((event.target as HTMLElement)?.closest('input,textarea,select,[contenteditable=true],[role=dialog]')) return;
      const digit = Number(event.key);
      if (digit >= 1 && digit <= 9 && digit <= tokens.length) {
        const token = tokens[digit - 1];
        setActiveToken(token);
        if (preview.ready) {
          event.preventDefault();
          void (preview.playing ? preview.input(token) : preview.inputAndStep(token));
        }
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [tokens, preview]);

  function fireToken(token: string) {
    setActiveToken(token);
    if (!preview.ready) return;
    void (preview.playing ? preview.input(token) : preview.inputAndStep(token));
  }

  return (
    <div className="combo-deck">
      <div className="panel-title">
        <b>连招试玩</b>
        <span className="grow"/>
        <span className="combo-deck-action">{currentSkill ?? '空闲'}</span>
        <span className="combo-deck-frame">{preview.state?.step ?? 0} 帧</span>
      </div>

      <div className="combo-deck-body">
        <div className="combo-pad-grid" role="group" aria-label="连招按键手柄">
          {tokens.map((token, idx) => {
            const color = inputColor(token);
            const label = inputShortLabel(project, token);
            const isSelected = token === selectedToken;
            return (
              <button key={token} type="button"
                className={`combo-pad-btn ${isSelected ? 'is-selected' : ''}`}
                style={{'--pad-color': color} as React.CSSProperties}
                disabled={!preview.ready}
                title={`${label} (${token})${idx < 9 ? ` · 快捷键 ${idx + 1}` : ''}`}
                onClick={() => fireToken(token)}>
                {idx < 9 && <kbd>{idx + 1}</kbd>}
                <span>{label}</span>
              </button>
            );
          })}
          {!tokens.length && <small className="combo-deck-empty">添加连招规则后即可试玩</small>}
        </div>

        <div className="combo-deck-controls">
          <button type="button" className={preview.playing ? '' : 'primary'} disabled={!preview.ready}
            onClick={() => preview.setPlaying(!preview.playing)}>
            {preview.playing ? <><Pause size={12}/>暂停</> : <><Play size={12}/>实时搓招</>}
          </button>
          <button type="button" disabled={!preview.ready || !selectedToken}
            title={`发送 ${selectedToken} 并前进 1 逻辑帧`}
            onClick={() => selectedToken && void preview.inputAndStep(selectedToken)}>
            <Zap size={12}/>输入并单步
          </button>
          <button type="button" disabled={!preview.ready || preview.playing} onClick={() => void preview.step()}>
            <SkipForward size={12}/>单步
          </button>
          <button type="button" disabled={(!preview.ready && !preview.error) || preview.placing}
            title="重置试放" onClick={() => void preview.reset()}>
            <RotateCcw size={12}/>
          </button>
        </div>

        <div className={`combo-deck-status ${preview.error ? 'is-error' : ''}`} role="status">
          {!project.skills.length ? '新建技能后可预览'
            : preview.error ? `配置无效 · ${preview.error}`
            : !preview.ready ? '正在应用配置…'
            : feedback || (preview.playing ? '运行中 · 按数字键 1–9 或点击按钮实时搓招' : '暂停中 · 点击按键直接单步试放')}
        </div>
      </div>
    </div>
  );
}
