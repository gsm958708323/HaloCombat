const fs=require('node:fs'),path=require('node:path');
function readProject(file){
  const index=JSON.parse(fs.readFileSync(file,'utf8'));if(!index.files)return index;
  const result={formatVersion:index.formatVersion,id:index.id,name:index.name};
  const read=relative=>JSON.parse(fs.readFileSync(path.join(path.dirname(file),relative),'utf8'));
  for(const [section,value] of Object.entries(index.files))result[section]=Array.isArray(value)?value.map(read):section==='runtime'?Object.fromEntries(Object.entries(value).map(([key,file])=>[key,read(file)])):read(value);
  // Mirror SkillProjectDiskLayout.Restore for sharded fixtures read without the host.
  const layout=result.editor?.nodePositions??[];
  const positions=new Map(layout.map(({kind,graphId,nodeId,x,y})=>[JSON.stringify([kind,graphId,nodeId]),{x,y}]));
  for(const kind of ['skills','buffs','timelines','projectiles','areas']){
    for(const graph of result[kind]??[])for(const node of graph.nodes){
      node.position=positions.get(JSON.stringify([kind,graph.id,node.id]))??node.position??{x:0,y:0};
    }
  }
  if(result.editor){
    delete result.editor.nodePositions;
    if(!Object.keys(result.editor).length)delete result.editor;
  }
  return result;
}
module.exports={readProject};
