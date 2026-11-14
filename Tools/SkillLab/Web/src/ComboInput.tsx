import {useState} from 'react';
import type {Project} from './model';

export function ComboInput({project,ready,send}:{project:Project;ready:boolean;send:(input:string)=>Promise<unknown>}){
  const [input,setInput]=useState('');const tokens=[...new Set(project.runtime?.combos.map(c=>c.input)??[])];
  if(!tokens.length)return null;const selected=tokens.includes(input)?input:tokens[0];
  return <div className="combo-input"><select aria-label="试验输入令牌" value={selected} onChange={e=>setInput(e.target.value)}>{tokens.map(token=><option key={token}>{token}</option>)}</select>
    <button disabled={!ready} onClick={()=>void send(selected)}>发送输入</button><small>走实际Combo；失败保留输入</small></div>;
}
