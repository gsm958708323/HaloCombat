import type {Field,ParameterValue} from '../model';

export function numericText(text:string,integer=false):number|null {
  if(!/^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(text.trim()))return null;
  const value=Number(text);
  return Number.isFinite(value)&&(!integer||Number.isInteger(value))?value:null;
}
export function displayNumber(value:number,field:Field):number {
  return Number((value*(field.displayScale??1)).toPrecision(12));
}
export function rangeBounds(value:number,field:Field) {
  const minimum=displayNumber(field.min??0,field),maximum=displayNumber(field.max??100,field);
  // Schema ranges guide dragging. A valid imported value must remain visible without clamping.
  return {min:Math.min(minimum,value),max:Math.max(maximum,value)};
}
export function rangeValue(value:number,field:Field):number {
  const scale=field.displayScale??1,step=(field.step??1)*scale,base=(field.min??0)*scale;
  const snapped=base+Math.round((value-base)/step)*step;
  const model=Number((snapped/scale).toPrecision(12));
  return field.integer?Math.round(model):model;
}
export function steppedValue(value:number,field:Field,direction:1|-1):number {
  return Number((value+direction*(field.step??1)).toPrecision(12));
}
export function formatFieldValue(field:Field,value:ParameterValue):string {
  return field.options?.find(([key])=>key===value)?.[1]??(typeof value==='number'?String(displayNumber(value,field)):Array.isArray(value)?value.join(', '):String(value));
}
