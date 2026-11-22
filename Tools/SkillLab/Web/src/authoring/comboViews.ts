import type {Project, RuntimeCombo} from '../model';

// Saved views still belong to the public JSON contract. Keep their references valid
// as rules are renamed, removed, or become ineligible for a legacy saved view.
export function maintainComboViews(project: Project, rename?: {from: string; to: string}): Project {
  const views = project.editor?.comboViews;
  if (!views) return project;
  const valid = new Set(project.runtime?.combos.filter(row => row.purpose !== 'cancel' && row.preSkills.length).map(row => row.id));
  return {...project, editor: {...project.editor, comboViews: views.map(view => ({
    ...view,
    ruleIds: [...new Set(view.ruleIds.map(id => rename && id === rename.from ? rename.to : id).filter(id => valid.has(id)))],
  }))}};
}
