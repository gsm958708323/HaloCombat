const fs=require('node:fs'),path=require('node:path');
function readProject(file){
  const index=JSON.parse(fs.readFileSync(file,'utf8'));if(!index.files)return index;
  const result={formatVersion:index.formatVersion,id:index.id,name:index.name};
  const read=relative=>JSON.parse(fs.readFileSync(path.join(path.dirname(file),relative),'utf8'));
  for(const [section,value] of Object.entries(index.files))result[section]=Array.isArray(value)?value.map(read):section==='runtime'?Object.fromEntries(Object.entries(value).map(([key,file])=>[key,read(file)])):read(value);
  return result;
}
module.exports={readProject};
