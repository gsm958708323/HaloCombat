import type {Project} from '../model';

export function inputLabel(project: Project, input: string) {
  const actions = project.runtime?.arena?.bindings.filter(binding => binding.input === input).map(binding => binding.action) ?? [];
  return actions.length ? actions.join(' / ') + ' · ' + input : input;
}
