import { clone } from '../model';
import type { Project } from '../model';

export class EditorHistory {
  current:Project;
  private past:Project[]=[];
  private future:Project[]=[];
  private group='';
  constructor(project:Project){this.current=project;}
  get canUndo(){return this.past.length>0;}
  get canRedo(){return this.future.length>0;}
  endGroup(){this.group='';}
  edit(next:Project,group='',remember=true){
    if(JSON.stringify(next)===JSON.stringify(this.current))return false;
    if(remember){
      if(!group||group!==this.group){this.past.push(clone(this.current));if(this.past.length>60)this.past.shift();}
      this.future=[];
    }
    this.group=remember?group:'';this.current=next;return true;
  }
  undo(){const value=this.past.pop();if(!value)return false;this.future.push(clone(this.current));this.current=value;this.endGroup();return true;}
  redo(){const value=this.future.pop();if(!value)return false;this.past.push(clone(this.current));this.current=value;this.endGroup();return true;}
}
