import type {Project} from '../model';

// Keep editor combo view & cancel bar references valid when rules are renamed, removed, or emptied.
export function maintainComboViews(project: Project, rename?: {from: string; to: string}): Project {
  const editor = project.editor;
  if (!editor?.comboViews && !editor?.comboCancelRuleIds) return project;
  const valid = new Set(project.runtime?.combos.filter(row => row.preSkills.length > 0).map(row => row.id));
  const mapIds = (ids: string[]) => [...new Set(ids.map(id => rename && id === rename.from ? rename.to : id).filter(id => valid.has(id)))];
  const nextCancelIds = editor.comboCancelRuleIds ? mapIds(editor.comboCancelRuleIds) : undefined;
  return {
    ...project,
    editor: {
      ...editor,
      ...(editor.comboViews ? {comboViews: editor.comboViews.map(view => ({...view, ruleIds: mapIds(view.ruleIds)}))} : {}),
      ...(nextCancelIds ? {comboCancelRuleIds: nextCancelIds.length ? nextCancelIds : undefined} : {}),
    },
  };
}
