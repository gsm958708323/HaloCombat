import { useRef, useState } from 'react';
import type { RefObject } from 'react';
import { Dialog } from './Dialog';
import type { Project } from './model';
import { publicationSlotLabel, publicationSlots } from './publicationSlots';
import type { PublicationSlot } from './publicationSlots';

type PublishRequest = { slot: PublicationSlot; skillId: string; body: string };
type InvalidSlot = { slot: PublicationSlot; reason: string };
type Confirmation = { request: PublishRequest; revision: string; slots: InvalidSlot[] };

export function PublicationActions({current,skillId,disabled,report}: {
  current: RefObject<Project|null>;
  skillId?: string;
  disabled: boolean;
  report: (text: string, error?: boolean) => void;
}) {
  const sending=useRef(false);
  const [busy,setBusy]=useState(false);
  const [confirmation,setConfirmation]=useState<Confirmation|null>(null);

  async function publish(request: PublishRequest, confirmed?: Confirmation) {
    if(sending.current)return;
    sending.current=true;
    setBusy(true);
    report(`正在发布到${publicationSlotLabel(request.slot)}…`);
    const query=new URLSearchParams({skillId:request.skillId});
    const headers:Record<string,string>={'Content-Type':'application/json'};
    if(confirmed){
      headers['If-Match']=confirmed.revision;
      for(const entry of confirmed.slots)query.append('clearInvalidSlots',entry.slot);
    }
    try{
      const response=await fetch(`/api/publish/${request.slot}?${query}`,{method:'POST',headers,body:request.body});
      const result=await response.json().catch(()=>null);
      if(response.status===409&&result?.groupRevision&&result.invalidSlots?.length){
        setConfirmation({request,revision:result.groupRevision,slots:result.invalidSlots});
        report(result.error ?? '请确认是否清空失效槽位后继续发布。');
        return;
      }
      setConfirmation(null);
      if(!response.ok)throw new Error(result?.error??`发布失败（HTTP ${response.status}）`);
      report(`${result.message} · ${result.skillId} · ${result.revision.slice(0,8)}`);
    }catch(error){
      setConfirmation(null);
      report(error instanceof Error ? error.message : String(error), true);
    }finally{
      sending.current=false;
      setBusy(false);
    }
  }

  function cancelConfirmation() {
    setConfirmation(null);
    report('已取消槽位发布。');
  }

  function begin(slot: PublicationSlot){
    if(current.current&&skillId)void publish({slot,skillId,body:JSON.stringify(current.current)});
  }

  return <>
    {publicationSlots.map(slot=><button key={slot} disabled={!skillId||disabled||busy||!!confirmation} onClick={()=>begin(slot)}>
      发布到{publicationSlotLabel(slot)}
    </button>)}
    {confirmation&&<Dialog label="清空失效槽位并发布" close={cancelConfirmation} busy={busy}>
      <div className="modal settings-dialog">
        <h2>清空失效槽位并发布</h2>
        <p>发布到{publicationSlotLabel(confirmation.request.slot)}时会更新其余有效槽位，并清空下列槽位。</p>
        <ul>{confirmation.slots.map(({slot,reason})=><li key={slot}><strong>{publicationSlotLabel(slot)}</strong> · {reason}</li>)}</ul>
        <div>
          <button disabled={busy} onClick={cancelConfirmation}>取消</button>
          <button className="primary" disabled={busy} onClick={()=>void publish(confirmation.request,confirmation)}>
            {busy?'发布中…':'清空并发布'}
          </button>
        </div>
      </div>
    </Dialog>}
  </>;
}
