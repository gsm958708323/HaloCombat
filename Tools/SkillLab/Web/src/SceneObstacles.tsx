import type { ObstacleView, Project } from './model';

export function SceneObstacles({project,update}:{project:Project;update:(project:Project)=>void}){
  const obstacles=project.preview.obstacles??[];
  function change(next:ObstacleView[]){update({...project,preview:{...project.preview,obstacles:next}});}
  function field(id:string,key:'x'|'z'|'width'|'depth',text:string){
    if(text==='')return;
    change(obstacles.map(obstacle=>obstacle.id===id?{...obstacle,[key]:Number(text)}:obstacle));
  }
  return <details className="scene-obstacles"><summary>试验障碍 · {obstacles.length}</summary>
    <div className="obstacle-controls"><button onClick={()=>change([...obstacles,{id:crypto.randomUUID(),x:-2,z:0,width:.5,depth:3}])}>添加障碍</button>
      {obstacles.map((obstacle,index)=><fieldset key={obstacle.id}><legend>障碍 {index+1}</legend>
        <div className="obstacle-fields">{([['x','X'],['z','Z'],['width','宽'],['depth','深']] as const).map(([key,label])=><label key={key}>{label}
          <input aria-label={`障碍${index+1}${label}`} type="number" step=".1" min={key==='width'||key==='depth'?.1:undefined} value={obstacle[key]} onChange={event=>field(obstacle.id,key,event.target.value)}/>
        </label>)}</div><button aria-label={`移除障碍${index+1}`} onClick={()=>change(obstacles.filter(value=>value.id!==obstacle.id))}>移除</button>
      </fieldset>)}
    </div>
  </details>;
}
