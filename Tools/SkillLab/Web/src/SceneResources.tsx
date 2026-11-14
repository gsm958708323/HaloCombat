import type { Project } from './model';

export function SceneResources({ project, update }: { project: Project; update: (p: Project) => void }) {
  const scene = project.preview;
  function value(key: 'energy' | 'initialEnergy' | 'recovery' | 'ammo' | 'initialAmmo', text: string) {
    if (text === '') return;
    update({ ...project, preview: { ...scene, [key]: Number(text) } });
  }
  return <details className="scene-resources"><summary>试验资源</summary><div>
    <label>能量上限<input aria-label="能量上限" type="number" min="0" value={scene.energy} onChange={e => value('energy', e.target.value)} /></label>
    <label>初始能量<input aria-label="初始能量" type="number" min="0" max={scene.energy} value={scene.initialEnergy ?? scene.energy} onChange={e => value('initialEnergy', e.target.value)} /></label>
    <label>每秒恢复<input aria-label="每秒恢复" type="number" min="0" step=".1" value={scene.recovery} onChange={e => value('recovery', e.target.value)} /></label>
    <button onClick={() => update({ ...project, preview: { ...scene, initialEnergy: null } })}>初始满能量</button>
    <label>弹药上限<input aria-label="弹药上限" type="number" min="0" max="1000000" step="1" value={scene.ammo??60} onChange={e=>value('ammo',e.target.value)}/></label>
    <label>初始弹药<input aria-label="初始弹药" type="number" min="0" max={scene.ammo??60} step="1" value={scene.initialAmmo??scene.ammo??60} onChange={e=>value('initialAmmo',e.target.value)}/></label>
    <button onClick={()=>update({...project,preview:{...scene,initialAmmo:null}})}>初始满弹药</button>
  </div></details>;
}
