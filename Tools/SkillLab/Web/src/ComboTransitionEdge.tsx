import {BaseEdge} from '@xyflow/react';
import type {EdgeProps} from '@xyflow/react';

export function ComboTransitionEdge({sourceX, sourceY, targetX, targetY, data, ...props}: EdgeProps) {
  const middleX = (sourceX + targetX) / 2;
  const lane = Number(data?.lane ?? 0);
  const count = Number(data?.laneCount ?? 1);
  const spread = (lane - (count - 1) / 2) * 44;
  const route = data?.route;
  // Forward and return ports face the same empty strip. Keeping both controls
  // between the ports keeps the entire Bézier curve out of the card columns.
  const loop = route === 'loop';
  const lift = loop ? 16 + lane * 20 : route === 'return' ? 38 + lane * 40 : -spread;
  const controlX1 = loop ? sourceX : middleX;
  const controlX2 = loop ? targetX : middleX;
  const path = `M${sourceX},${sourceY} C${controlX1},${sourceY - lift} ${controlX2},${targetY - lift} ${targetX},${targetY}`;
  const labelY = (sourceY + targetY) / 2 - lift * .75;
  return <BaseEdge id={props.id} path={path} markerEnd={props.markerEnd} label={props.label}
    labelX={middleX} labelY={labelY} interactionWidth={14}/>;
}
