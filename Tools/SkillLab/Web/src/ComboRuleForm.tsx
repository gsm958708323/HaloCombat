import {useLayoutEffect, useRef, useState} from 'react';
import type {Project, RuntimeCombo} from './model';
import {prepareComboEdit} from './authoring/comboEdits';

type Props = {
  project: Project;
  row?: RuntimeCombo;
  initial: RuntimeCombo;
  update: (edit: (project: Project) => Project, remember?: boolean, group?: string) => void;
  committed: (previous: RuntimeCombo | undefined, next: RuntimeCombo | null) => void;
  cancelNew: () => void;
  report: (message: string) => void;
};

export function ComboRuleForm({project, row, initial, update, committed, cancelNew, report}: Props) {
  const latest = useRef(project);
  latest.current = project;
  const request = useRef<AbortController | null>(null);
  useLayoutEffect(() => () => request.current?.abort(), []);
  const baseline = JSON.stringify(initial, null, 2);
  const [draft, setDraft] = useState<{text: string; baseline: string} | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const text = draft?.text ?? baseline;
  const stale = !!draft && draft.baseline !== baseline;

  async function apply(deleting: boolean) {
    if (request.current) return;
    const controller = new AbortController();
    request.current = controller;
    const before = latest.current;
    try {
      setBusy(true);
      setError('');
      if (stale && !deleting) throw new Error('工程中的规则已变更，请读取当前规则后再应用');
      const candidate = prepareComboEdit(before, row?.id, deleting ? null : text);
      const response = await fetch('/api/validate', {method: 'POST', headers: {'Content-Type': 'application/json'},
        body: candidate.json, signal: controller.signal});
      if (!response.ok) {
        const result = await response.json().catch(() => null);
        throw new Error(result?.error ?? result?.detail ?? `连招校验失败 (${response.status})`);
      }
      if (controller.signal.aborted) return;
      let applied = false;
      // Compare the editor's authoritative value, including edits before the next React render.
      update(current => {
        if (current !== before || controller.signal.aborted) return current;
        applied = true;
        return candidate.project;
      }, true, '');
      if (!applied) throw new Error('校验期间工程已改变，请检查规则后重新应用');
      setDraft(null);
      setConfirmDelete(false);
      committed(row, candidate.rule);
      report(deleting ? '已删除接续规则' : candidate.rule?.preSkills.length ? '已应用接续规则' : '已保存为空前置规则，此规则不显示在连招图中');
    } catch (error) {
      if (!controller.signal.aborted) setError(error instanceof Error ? error.message : String(error));
    } finally {
      request.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }

  return <div className="combo-rule-editor">
    <label htmlFor="combo-rule-json">ComboEntry JSON</label>
    <textarea id="combo-rule-json" spellCheck={false} value={text} disabled={busy}
      onChange={event => { setDraft({text: event.target.value, baseline: draft?.baseline ?? baseline}); setConfirmDelete(false); }}/>
    {stale && <p role="status">规则已变更。可复制草稿，再读取工程中的规则。</p>}
    <div className="combo-rule-actions">
      <button type="button" className="primary" disabled={busy || stale} onClick={() => void apply(false)}>{busy ? '校验中…' : '应用规则'}</button>
      {row ? <>
        <button type="button" disabled={busy} onClick={() => { setDraft(null); setError(''); setConfirmDelete(false); }}>读取当前规则</button>
        <button type="button" disabled={busy} onClick={() => confirmDelete ? void apply(true) : setConfirmDelete(true)}>{confirmDelete ? '确认删除规则' : '删除规则'}</button>
        {confirmDelete && <button type="button" disabled={busy} onClick={() => setConfirmDelete(false)}>取消删除</button>}
      </> : <button type="button" onClick={cancelNew}>取消新增</button>}
    </div>
    {error && <p className="combo-rule-error" role="alert">{error}</p>}
  </div>;
}
