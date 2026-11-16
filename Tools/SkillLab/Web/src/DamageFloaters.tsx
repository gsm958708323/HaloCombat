import {useEffect,useRef,useState} from 'react';
import type {ActorView,RunEvent} from './model';
import './preview-effects.css';

type Floater={id:number;x:number;z:number;amount:number;critical:boolean;until:number};
export function DamageFloaters({events,actors,sessionId}:{events:RunEvent[];actors:ActorView[];sessionId:string}){
  const [items,setItems]=useState<Floater[]>([]),last=useRef(0),session=useRef(sessionId);
  useEffect(()=>{
    if(session.current!==sessionId){session.current=sessionId;last.current=0;setItems([]);}
    const now=performance.now(),added:Floater[]=[];
    for(const event of events){
      if(event.sequence<=last.current)continue;last.current=event.sequence;
      if(!event.damage||!['damage','critical'].includes(event.kind))continue;
      const amount=event.damage.absorbed+event.damage.hpLost;if(amount<=0)continue;
      const target=actors.find(a=>a.id===event.targetId||a.name===event.damage!.target);
      const x=event.targetX??target?.x,z=event.targetZ??target?.z;if(x===undefined||z===undefined)continue;
      added.push({id:event.sequence,x:x+(event.sequence%3-1)*.16,z:z+Math.max(.7,target?.radius??.4)+.6,amount,critical:event.kind==='critical',until:now+1100});
    }
    if(added.length)setItems(previous=>[...previous.filter(i=>i.until>now),...added].slice(-96));
  },[events,actors,sessionId]);
  useEffect(()=>{
    if(!items.length)return;const timer=setTimeout(()=>setItems(previous=>previous.filter(i=>i.until>performance.now())),Math.max(20,Math.min(...items.map(i=>i.until))-performance.now()));
    return()=>clearTimeout(timer);
  },[items]);
  return <g className="damage-floaters" pointerEvents="none" aria-label="命中伤害飘字">{items.map(item=><g key={item.id} transform={`translate(${item.x},${-item.z})`}>
    <text className={item.critical?'damage-floater critical':'damage-floater'} textAnchor="middle" data-amount={item.amount}>{item.amount.toLocaleString('zh-CN',{maximumFractionDigits:1})}{item.critical?'!':''}</text>
  </g>)}</g>;
}
