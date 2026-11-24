import {useMemo, useState} from 'react';
import {Plus, Search, X} from 'lucide-react';
import type {Project} from './model';
import {skillMeta} from './authoring/comboModel';

type Props = {
  project: Project;
  onCanvasIds: ReadonlySet<string>;
  onAddSkill: (skillId: string) => void;
  onClose: () => void;
};

export function ComboSkillDrawer({project, onCanvasIds, onAddSkill, onClose}: Props) {
  const [query, setQuery] = useState('');
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return project.skills.filter(skill =>
      !q || skill.name.toLowerCase().includes(q) || skill.id.toLowerCase().includes(q) || (skill.category ?? '').toLowerCase().includes(q),
    );
  }, [project.skills, query]);

  return (
    <div className="combo-skill-drawer" role="region" aria-label="技能库抽屉">
      <header className="combo-drawer-head">
        <strong>技能库</strong>
        <small>点击或拖入画布</small>
        <button type="button" className="icon-btn" aria-label="关闭技能库" onClick={onClose}><X size={13}/></button>
      </header>
      <div className="combo-drawer-search">
        <Search size={12}/>
        <input value={query} placeholder="搜索技能名或 ID…" autoFocus onChange={e => setQuery(e.target.value)}/>
      </div>
      <div className="combo-drawer-list">
        {filtered.map(skill => {
          const meta = skillMeta(project, skill);
          const onCanvas = onCanvasIds.has(skill.id);
          return (
            <div key={skill.id} className={`combo-drawer-item ${onCanvas ? 'is-on-canvas' : ''}`}
              draggable
              onDragStart={event => {
                event.dataTransfer.setData('application/x-halocombat-skill', skill.id);
                event.dataTransfer.effectAllowed = 'copy';
              }}
              onClick={() => onAddSkill(skill.id)}>
              <span className="combo-drawer-swatch" style={{background: meta.accent}}/>
              <div className="combo-drawer-info">
                <strong>{skill.name}</strong>
                <small>{meta.typeLabel} · {skill.id}</small>
              </div>
              {onCanvas ? <span className="combo-drawer-badge">已在画布</span> : <Plus size={13} className="combo-drawer-add"/>}
            </div>
          );
        })}
        {!filtered.length && <p className="combo-drawer-empty">没有匹配的技能</p>}
      </div>
    </div>
  );
}
