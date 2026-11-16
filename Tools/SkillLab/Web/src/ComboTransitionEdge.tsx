import {BaseEdge,getBezierPath} from '@xyflow/react';
import type {EdgeProps} from '@xyflow/react';

export function ComboTransitionEdge(props:EdgeProps){
  const {sourceX:sx,sourceY:sy,targetX:tx,targetY:ty}=props;
  const lane=Number(props.data?.lane??0);
  // Reverse transitions and self loops arc above the nodes, keeping their
  // direction readable even when both endpoint handles share one horizontal row.
  const rise=135+lane*45;
  const [path,x,y]=sx>=tx?[`M${sx},${sy} C${sx+110},${sy-rise} ${tx-110},${ty-rise} ${tx},${ty}`,(sx+tx)/2,(sy+ty)/2-rise*.75]:
    lane>0?[`M${sx},${sy} C${sx+30},${sy-60*lane} ${tx-30},${ty-60*lane} ${tx},${ty}`,(sx+tx)/2,(sy+ty)/2-45*lane]:getBezierPath(props);
  return <BaseEdge id={props.id} path={path} markerEnd={props.markerEnd} style={props.style} label={props.label} labelX={x} labelY={y}
    labelStyle={props.labelStyle} labelBgStyle={props.labelBgStyle} interactionWidth={20}/>;
}
