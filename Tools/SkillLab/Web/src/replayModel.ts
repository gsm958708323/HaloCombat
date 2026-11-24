import type {PreviewState} from './model';
export interface RecordingStatus {enabled:boolean;inputs:number;steps:number;error:string|null}
export interface ReplayComparison {matches:boolean;values:number;tolerance:number;maximumNumericDifference:number;differences:{path:string;expected:string;actual:string}[]}
export interface ReplayResponse {id:string;state:PreviewState;recording:RecordingStatus;comparison:ReplayComparison;projectJson:string;rootSkillId:string}
