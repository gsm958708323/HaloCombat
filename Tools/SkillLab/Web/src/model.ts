export type Parameters = Record<string, string | number | boolean>;
export interface GraphNode { id: string; kind: string; runtimeId?: number; enabled?: boolean; position: { x: number; y: number }; parameters: Parameters }
export interface GraphEdge { id: string; source: string; sourcePort: string; target: string; targetPort: string; order: number }
export interface TimelineClip {id:string;kind:'cancel'|'move'|'hitbox'|'iframe';start:number;end:number;moveX?:number;moveY?:number;moveZ?:number;steer?:number;hitRadius?:number;hitOffsetX?:number;hitOffsetY?:number;hitOffsetZ?:number;effects?:string[]}
export interface TimelinePayload {id:string;time:number;repeatInterval?:number;effects:string[]}
export interface AuthoredTimeline {runtimeId:number;duration:number;clips:TimelineClip[];payloads:TimelinePayload[]}
export interface RuntimeCombo {id:string;preSkills:string[];input:string;requiredTags:number[];priority:number;toSkill:string}
export interface RuntimeActor {id:string;role:string;team:number;skillId?:string;dodgeSkill?:string;slots?:string[];view:string;[key:string]:unknown}
export interface ArenaSettings {player:string;enemy:string;trainingTarget:string;seed:number;maxEnemies:number;spawnPeriod:number;cleanupDelay:number;halfWidth:number;halfDepth:number;spawnDistance:number;playerX:number;playerZ:number;playerYaw:number;cameraDistance:number;cameraHeight:number;cameraFov:number;cameraFocusHeight:number;bindings:{action:string;input:string}[];obstacles?:ObstacleView[]}
export interface RuntimeContent {motor:{gravity:number;jumpSpeed:number;airSteer:number;groundY:number;stickDeadzone:number};combos:RuntimeCombo[];actors:RuntimeActor[];cues:{id:number;key:string;kind:string;resource?:string;sound?:string;lifetime:number}[];arena?:ArenaSettings}
export interface Skill { id: string; runtimeId: number; name: string; category: string; nodes: GraphNode[]; edges: GraphEdge[];timeline?:AuthoredTimeline }
export interface BuffModifier {attribute:string;operation:string;value:number;priority:number}
export interface BuffDefinition {id:string;runtimeId:number;name:string;permanent:boolean;duration:number;interval:number;stack:string;maxStacks:number;mutexGroup:number;tags:number[];modifiers:BuffModifier[];nodes:GraphNode[];edges:GraphEdge[]}
export interface ActorInitial { id: string; x: number; z: number; hp: number; atk: number; def: number; shield?:number;damageDealt?:number;damageTaken?:number;collisionRadius?: number }
export interface ObstacleView { id: string; x: number; z: number; width: number; depth: number }
export interface RayView { sequence: number; step: number; fromX: number; fromZ: number; toX: number; toZ: number; width: number; hits: number }
export interface SummonBlueprint { id: string; name: string; skillId: string; hp: number; attack: number; defense: number; moveSpeed: number; collisionRadius: number; energy: number; initialEnergy?: number | null; recovery: number; ammo?:number;initialAmmo?:number|null;acquireRadius: number; attackRange: number; followRange: number; holdDuration: number }
export interface GraphViewport { kind:'skill'|'buff';id:string;x:number;y:number;zoom:number }
export interface EditorMetadata { selectedSkill?:string;selectedBuff?:string;viewports?:GraphViewport[] }
export interface Project { formatVersion: number; id: string; name: string; skills: Skill[]; buffs?:BuffDefinition[]; summonBlueprints?: SummonBlueprint[];editor?:EditorMetadata;runtime?:RuntimeContent; preview: { seed: number; energy: number; initialEnergy?: number | null; recovery: number; ammo?:number;initialAmmo?:number|null;caster: ActorInitial; targets: ActorInitial[]; obstacles?: ObstacleView[] } }
export interface BuffView {instanceId:number;id:string;name:string;source:string;stacks:number;remaining:number|null}
export interface ActorView { id: string; name: string; kind: string; x: number; y:number; z: number; hp: number | null; maxHp: number | null; radius: number; remaining: number | null; dead: boolean; owner: string | null; blueprint: string | null; skill: string | null; energy: number | null;ammo:number|null;trackingKey:string|null;castId:number;generation:number;chainsLeft:number;distance:number|null;range:number|null;speed:number|null;verticalSpeed:number|null;returning:boolean;groundBouncesLeft:number;wallBouncesLeft:number;buffs:BuffView[];tags:Record<string,number>;attributes:Record<string,number> }
export interface AreaView { sequence: number; step: number; x: number; z: number; radius: number; targets: number }
export interface VolumeView { sequence: number; source: string; shape: string; x: number; z: number; yaw: number; length: number; width: number; angle: number }
export interface DamageBreakdown {kind:string;target:string;base:number;scaled:number;afterDefense:number;afterCritical:number;final:number;absorbed:number;hpLost:number;overkill:number;killed:boolean}
export interface RunEvent { sequence: number; time: number; kind: string; source: string; result: string;castId:number;skill:string|null;hitIndex:number;damage:DamageBreakdown|null }
export interface PreviewState { step: number; time: number; actors: ActorView[]; events: RunEvent[]; areas: AreaView[]; volumes: VolumeView[]; obstacles: ObstacleView[]; rays: RayView[]; trails: { entity: string; x: number; z: number; step: number }[]; energy: number; ammo:number;ammoCapacity:number;channeling: boolean; charging: boolean; chargeProgress: number; castPower: number; cooldowns: Record<string, number>; statistics: { damage: number; hits: number; casts: number; projectiles: number; chains:number; splits:number; areas: number; summons: number } }
export interface Field { key: string; label: string; type?: 'select' | 'check' | 'text'; unit?: string; min?: number; max?: number; step?: number; options?: [string, string][]; initial: number | string | boolean; when?: (parameters: Parameters) => boolean }
export const kinds: Record<string, { title: string; color: string; fields: Field[]; inputs: string[]; outputs: [string, string][] }> = {
  target: { title: '目标选择', color: '#526c7b', inputs: [], outputs: [['target', '目标 / 方向']], fields: [
    { key: 'mode', label: '目标选择', type: 'select', initial: 'point', options: [['point','指定位置'],['entity','指定目标'],['nearest','最近目标'],['random','随机目标'],['direction','随机方向'],['self','施法者自身'],['facing','当前朝向']] },
    { key: 'range', label: '索敌距离', unit: '米', min: .1, max: 50, step: .1, initial: 20 }
  ] },
  cast: { title: '施放规则', color: '#5b7164', inputs: ['target'], outputs: [['cast','施放']], fields: [
    { key: 'mode', label: '施放规则', type: 'select', initial: 'instant', options: [['instant','瞬发'],['channel','引导'],['charge','蓄力']] },
    { key: 'cost', label: '能量消耗', min: 0, max: 100, step: 1, initial: 10 },
    {key:'ammoCost',label:'弹药消耗',min:0,max:1000000,step:1,initial:0},
    {key:'fallbackSkill',label:'缺弹替代技能',type:'select',initial:''},
    {key:'recastProjectile',label:'重施放追踪键',type:'text',initial:''},
    {key:'recastSkill',label:'弹体存在时技能',type:'select',initial:''},
    {key:'requiredProjectile',label:'必需弹体追踪键',type:'text',initial:''},
    { key: 'cooldown', label: '冷却', unit: '秒', min: 0, max: 10, step: .05, initial: .7 },
    { key: 'delay', label: '延迟生效', unit: '秒', min: 0, max: 3, step: .02, initial: .1 },
    { key: 'duration', label: '动作总长', unit: '秒', min: .02, max: 5, step: .02, initial: .3, when: p => p.mode !== 'channel' },
    { key: 'count', label: '连发次数', min: 1, max: 1000, step: 1, initial: 1, when: p => p.mode !== 'channel' && p.mode !== 'charge' },
    { key: 'interval', label: '生效间隔', unit: '秒', min: .001, max: 1, step: .001, initial: .1, when: p => p.mode !== 'charge' },
    { key: 'costPerPulse', label: '逐段支付资源', type: 'check', initial: false, when: p => p.mode !== 'channel' && p.mode !== 'charge' },
    { key: 'energyPerSecond', label: '持续耗能', unit: '/秒', min: 0, max: 100, step: 1, initial: 12, when: p => p.mode === 'channel' || p.mode === 'charge' },
    { key: 'sustained', label: '持续引导', type: 'check', initial: true, when: p => p.mode === 'channel' },
    { key: 'channelDuration', label: '引导时长', unit: '秒', min: .02, max: 120, step: .02, initial: 3, when: p => p.mode === 'channel' && p.sustained === false },
    { key: 'chargeMin', label: '最小蓄力', unit: '秒', min: 0, max: 5, step: .02, initial: .2, when: p => p.mode === 'charge' },
    { key: 'chargeMax', label: '最大蓄力', unit: '秒', min: .02, max: 10, step: .02, initial: 1, when: p => p.mode === 'charge' },
    { key: 'powerMin', label: '最低倍率', unit: '×', min: .01, max: 10, step: .1, initial: 1, when: p => p.mode === 'charge' },
    { key: 'powerMax', label: '满蓄倍率', unit: '×', min: .01, max: 10, step: .1, initial: 3, when: p => p.mode === 'charge' },
    { key: 'autoRelease', label: '满蓄自动释放', type: 'check', initial: true, when: p => p.mode === 'charge' }
  ] },
  projectile: { title: '效果传递', color: '#887547', inputs: ['execute'], outputs: [['hit','命中结算'],['wall','撞墙终结'],['range','射程耗尽'],['lifetime','寿命结束'],['bounce','成功反弹'],['returned','返回来源'],['ground','落地终结'],['expire','投射物结束']], fields: [
    {key:'trackingKey',label:'来源追踪键',type:'text',initial:''},
    {key:'trajectory',label:'飞行轨迹',type:'select',initial:'linear',options:[['linear','方向飞行'],['accelerate','加减速'],['ramp','渐进加速曲线'],['return','回旋返回'],['bounce','地面弹跳']]},
    {key:'rampTime',label:'加速时间尺度',unit:'秒',min:.001,max:3600,step:.01,initial:5,when:p=>p.trajectory==='ramp'},
    { key: 'speed', label: '飞行速度', unit: '米/秒', min: .1, max: 60, step: .1, initial: 12 },
    {key:'acceleration',label:'加速度',unit:'米/秒²',min:-1000,max:1000,step:.1,initial:6,when:p=>p.trajectory==='accelerate'},
    {key:'maxSpeed',label:'速度上限',unit:'米/秒',min:.1,max:1000,step:.1,initial:60,when:p=>p.trajectory==='accelerate'},
    {key:'returnAfter',label:'开始返回时间',unit:'秒',min:0,max:3600,step:.01,initial:.6,when:p=>p.trajectory==='return'},
    {key:'returnSpeed',label:'返回速度',unit:'米/秒',min:.1,max:1000,step:.1,initial:12,when:p=>p.trajectory==='return'},
    {key:'catchRadius',label:'返回收取半径',unit:'米',min:.01,max:10,step:.01,initial:.4,when:p=>p.trajectory==='return'},
    {key:'launchHeight',label:'发射高度',unit:'米',min:0,max:100,step:.01,initial:.5,when:p=>p.trajectory==='bounce'},
    {key:'verticalSpeed',label:'初始竖直速度',unit:'米/秒',min:0,max:100,step:.1,initial:5,when:p=>p.trajectory==='bounce'},
    {key:'gravity',label:'弹跳重力',unit:'米/秒²',min:.1,max:1000,step:.1,initial:20,when:p=>p.trajectory==='bounce'},
    {key:'restitution',label:'反弹恢复系数',min:0,max:1,step:.01,initial:.6,when:p=>p.trajectory==='bounce'},
    {key:'groundBounces',label:'额外地面反弹次数',min:0,max:32,step:1,initial:2,when:p=>p.trajectory==='bounce'},
    {key:'wallBounces',label:'额外撞墙反射次数',min:0,max:64,step:1,initial:0},
    { key: 'range', label: '飞行距离 (0=寿命限制)', unit: '米', min: 0, max: 60, step: .1, initial: 24 },
    { key:'spawnForward',label:'枪口前移',unit:'米',min:0,max:10,step:.01,initial:.4 },
    { key: 'lifetime', label: '最大寿命', unit: '秒', min: .001, max: 3600, step: .001, initial: 120 },
    { key: 'radius', label: '碰撞半径', unit: '米', min: .01, max: 2, step: .01, initial: .18 },
    { key: 'multi', label: '多重发射', type: 'check', initial: false },
    { key: 'count', label: '同时发射数量', min: 1, max: 64, step: 1, initial: 3, when:p=>p.multi===true },
    { key: 'spread', label: '散射角度', unit:'°', min: 0, max: 360, step: 1, initial: 45, when:p=>p.multi===true },
    { key: 'maxHits', label: '每段命中上限 (0=无限穿透)', min: 0, max: 10000, step: 1, initial: 1 },
    { key: 'chain', label: '命中后连锁', type: 'check', initial:false },
    { key: 'chainCount', label: '额外连锁次数', min: 1, max: 64, step: 1, initial: 2, when:p=>p.chain===true },
    { key: 'chainRadius', label: '连锁搜索距离', unit:'米', min:.01,max:1000,step:.01,initial:6,when:p=>p.chain===true },
    { key: 'chainMode', label: '连锁选敌', type:'select',initial:'nearest',options:[['nearest','最近目标'],['random','随机目标']],when:p=>p.chain===true },
    { key: 'homingRate', label: '追踪转速', unit: '度/秒', min: 0, max: 720, step: 10, initial: 0 },
    { key: 'homingRetarget', label: '追踪丢失后重选', type:'check', initial:false,when:p=>Number(p.homingRate)>0 },
    { key: 'homingRadius', label: '追踪搜索距离', unit:'米', min:.01,max:1000,step:.01,initial:12,when:p=>Number(p.homingRate)>0 }
  ] },
  split: {title:'命中分裂',color:'#8b7352',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'count',label:'分裂数量',min:1,max:32,step:1,initial:3},
    {key:'spread',label:'分裂夹角',unit:'°',min:0,max:360,step:1,initial:60},
    {key:'generations',label:'最大分裂代数',min:0,max:8,step:1,initial:1},
    {key:'powerMultiplier',label:'子弹伤害倍率',unit:'×',min:.001,max:10,step:.001,initial:.5},
    {key:'rangeMode',label:'子弹射程模式',type:'select',initial:'inherit',options:[['inherit','继承父弹剩余距离'],['fixed','固定距离']]},
    {key:'range',label:'子弹飞行距离',unit:'米',min:.01,max:1000,step:.01,initial:8,when:p=>p.rangeMode==='fixed'}
  ]},
  damage: { title: '效果结算', color: '#65697b', inputs: ['execute'], outputs: [['next','继续']], fields: [
    {key:'damageType',label:'伤害类型',type:'select',initial:'physical',options:[['physical','物理'],['elemental','通用元素']]},
    {key:'includeBonuses',label:'计入加成',type:'check',initial:true},
    { key: 'scope', label: '结算范围', type: 'select', initial: 'target', options: [['target','当前目标'],['area','范围结算']] },
    { key: 'center', label: '范围中心', type: 'select', initial: 'point', options: [['point','事件 / 指定点'],['target','当前目标位置'],['source','来源位置']], when: p=>p.scope==='area' },
    { key: 'radius', label: '结算半径', unit: '米', min: .1, max: 20, step: .1, initial: 3, when:p=>p.scope==='area' },
    { key: 'maxTargets', label: '目标上限 (0=全部)', min: 0, max: 128, step: 1, initial: 0, when:p=>p.scope==='area' },
    { key: 'flat', label: '基础伤害', min: 0, max: 500, step: 1, initial: 80 },
    { key: 'coefficient', label: '攻击系数', unit: '×', min: 0, max: 5, step: .1, initial: 0 },
    { key:'canCrit',label:'允许暴击',type:'check',initial:true },
    { key:'critFromAttribute',label:'使用角色暴击率',type:'check',initial:false,when:p=>p.canCrit!==false },
    { key: 'critChance', label: '暴击概率', min: 0, max: 1, step: .01, initial: .15,when:p=>p.canCrit!==false&&p.critFromAttribute!==true },
    { key:'snapshotAttack',label:'使用攻击快照',type:'check',initial:true },
    { key: 'critMultiplier', label: '暴击倍率', unit: '×', min: 1, max: 5, step: .1, initial: 1.8 },
    { key: 'ignoreDefense', label: '忽略防御', type: 'check', initial: false },
    {key:'scaleByStacks',label:'按Buff层数缩放',type:'check',initial:false},
    {key:'notifyHurt',label:'通知受击Buff',type:'check',initial:true},
    { key: 'hitstopFrames', label: '命中顿帧', unit: '帧', min: 0, max: 10, step: 1, initial: 0 }
  ] },
  'skill-events':{title:'技能事件',color:'#765f79',inputs:[],outputs:[['hit','实际扣血命中'],['critical','暴击扣血'],['kill','击杀'],['completed','自然完成'],['wall','撞墙终结'],['range','射程耗尽'],['projectile-end','投射物结束']],fields:[]},
  'after-damage':{title:'扣血后条件',color:'#7d6d5c',inputs:['execute'],outputs:[['apply','符合条件'],['next','继续']],fields:[{key:'latestCastOnly',label:'仅来源最近施法',type:'check',initial:false}]},
  impact:{title:'受击 / 状态效果',color:'#86635e',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'mode',label:'效果类型',type:'select',initial:'hitstun',options:[['hitstun','受击硬直'],['knockback','击退'],['knockdown','击倒'],['iframe','无敌'],['launch','击飞'],['pull','向点拉动'],['shield','增加护盾']]},
    {key:'recipient',label:'作用对象',type:'select',initial:'target',options:[['target','当前目标'],['self','效果来源自身']]},
    {key:'duration',label:'效果持续时间',unit:'秒',min:.001,max:120,step:.001,initial:.3,when:p=>p.mode==='hitstun'||p.mode==='knockdown'||p.mode==='iframe'},
    {key:'iframe',label:'受击附加无敌',unit:'秒',min:0,max:120,step:.01,initial:0,when:p=>p.mode==='hitstun'},
    {key:'distance',label:'作用位移距离',unit:'米',min:0,max:100,step:.1,initial:2,when:p=>p.mode==='knockback'||p.mode==='pull'},
    {key:'speed',label:'击飞竖直速度',unit:'米/秒',min:.01,max:100,step:.01,initial:6,when:p=>p.mode==='launch'},
    {key:'center',label:'拉动中心',type:'select',initial:'source',options:[['point','事件指定点'],['source','效果来源'],['target','当前目标']],when:p=>p.mode==='pull'},
    {key:'amount',label:'护盾增加量',min:0,max:10000000,step:1,initial:100,when:p=>p.mode==='shield'}
  ]},
  cue: { title: '表现事件', color: '#74667e', inputs: ['execute'], outputs: [['next','继续']], fields: [{ key: 'id', label: 'Cue ID', min: 1, max: 9999, step: 1, initial: 1 }] },
  buff:{title:'施加 Buff',color:'#5c7e68',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'buff',label:'Buff定义',type:'select',initial:'',options:[]},
    {key:'stacks',label:'施加层数',min:1,max:128,step:1,initial:1},
    {key:'recipient',label:'持有者',type:'select',initial:'target',options:[['target','当前目标'],['self','效果来源自身']]}
  ]},
  dispel:{title:'驱散 Buff',color:'#6b7d85',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'mode',label:'驱散规则',type:'select',initial:'buff',options:[['buff','指定Buff'],['group','互斥组'],['tag','授予Tag'],['source','当前效果来源']]},
    {key:'buff',label:'Buff定义',type:'select',initial:'',options:[],when:p=>!p.mode||p.mode==='buff'},
    {key:'group',label:'互斥组',min:1,max:1000000,step:1,initial:1,when:p=>p.mode==='group'},
    {key:'tag',label:'授予Tag ID',min:1,max:1000000,step:1,initial:3101,when:p=>p.mode==='tag'},
    {key:'maxCount',label:'移除实例上限 (0=全部)',min:0,max:128,step:1,initial:0},
    {key:'recipient',label:'持有者',type:'select',initial:'target',options:[['target','当前目标'],['self','效果来源自身']]}
  ]},
  heal:{title:'恢复生命',color:'#597f76',inputs:['execute'],outputs:[['next','继续']],fields:[{key:'amount',label:'恢复量',min:0,max:1000000,step:1,initial:10}]},
  'buff-events':{title:'Buff 生命周期',color:'#537760',inputs:[],outputs:[['apply','首次施加'],['stack','实际增层'],['period','周期'],['expire','自然到期'],['hurt','持有者受击'],['cast','持有者施法'],['removed','移除清理']],fields:[]},
  delivery: { title: '目标 / 区域 / 动作', color: '#887547', inputs: ['execute'], outputs: [['apply','作用效果'],['enter','进入区域'],['exit','离开区域'],['stay','区域停留'],['expire','自然到期'],['wall','撞墙'],['complete','完成'],['removed','移除']], fields: [
    { key:'mode', label:'传递方式', type:'select', initial:'target', options:[['target','目标作用'],['area','区域作用'],['melee','近战判定'],['displacement','位移攻击'],['ray','射线判定'],['summon','召唤分身']] },
    { key:'recipient', label:'作用目标', type:'select', initial:'target', options:[['target','当前目标'],['self','施法者自身']],when:p=>!p.mode||p.mode==='target' },
    { key:'center',label:'作用中心',type:'select',initial:'point',options:[['point','事件 / 指定点'],['target','当前目标位置'],['source','来源位置'],['auto','指定点或来源']],when:p=>p.mode==='area'||p.mode==='summon' },
    { key:'radius',label:'作用半径',unit:'米',min:.1,max:20,step:.1,initial:3,when:p=>p.mode==='area' },
    { key:'maxTargets',label:'目标上限 (0=全部)',min:0,max:128,step:1,initial:0,when:p=>p.mode==='area'||p.mode==='melee'||p.mode==='displacement'||(p.mode==='ray'&&p.pierce===true) },
    { key:'persistent',label:'持续区域',type:'check',initial:false,when:p=>p.mode==='area' },
    { key:'permanent',label:'永久召唤',type:'check',initial:false,when:p=>p.mode==='summon' },
    { key:'duration',label:'持续时间',unit:'秒',min:.02,max:120,step:.02,initial:2,when:p=>(p.mode==='area'&&p.persistent===true)||(p.mode==='summon'&&p.permanent!==true) },
    { key:'interval',label:'脉冲间隔 (0=关闭)',unit:'秒',min:0,max:10,step:.001,initial:.5,when:p=>p.mode==='area'&&p.persistent===true },
    { key:'pulseOnSpawn',label:'生成时脉冲',type:'check',initial:true,when:p=>p.mode==='area'&&p.persistent===true },
    { key:'shape',label:'判定形状',type:'select',initial:'thrust',options:[['thrust','刺击'],['fan','扇形横扫'],['circle','360° 旋风']],when:p=>p.mode==='melee' },
    { key:'length',label:'攻击距离',unit:'米',min:.1,max:1000,step:.1,initial:3,when:p=>p.mode==='melee'||p.mode==='ray' },
    { key:'width',label:'判定宽度',unit:'米',min:.01,max:100,step:.01,initial:1,when:p=>p.mode==='ray'||(p.mode==='melee'&&(!p.shape||p.shape==='thrust')) },
    { key:'pierce',label:'射线穿透',type:'check',initial:false,when:p=>p.mode==='ray' },
    { key:'angle',label:'横扫角度',unit:'°',min:1,max:360,step:1,initial:90,when:p=>p.mode==='melee'&&p.shape==='fan' },
    { key:'startup',label:'启动前摇',unit:'秒',min:0,max:3,step:.01,initial:.1,when:p=>p.mode==='melee'||p.mode==='displacement' },
    { key:'activeDuration',label:'攻击时长',unit:'秒',min:.001,max:5,step:.001,initial:.2,when:p=>p.mode==='melee' },
    { key:'movementMode',label:'位移模式',type:'select',initial:'dash',options:[['dash','冲刺'],['blink','闪现']],when:p=>p.mode==='displacement' },
    { key:'distance',label:'移动距离 / 射程',unit:'米',min:.1,max:30,step:.1,initial:6,when:p=>p.mode==='displacement' },
    { key:'speed',label:'冲刺速度',unit:'米/秒',min:.1,max:100,step:.1,initial:12,when:p=>p.mode==='displacement'&&p.movementMode!=='blink' },
    { key:'hitRadius',label:'命中半径',unit:'米',min:.01,max:10,step:.01,initial:.5,when:p=>p.mode==='displacement' },
    { key:'blueprint',label:'召唤蓝图',type:'select',initial:'',options:[],when:p=>p.mode==='summon' },
    { key:'count',label:'生成数量',min:1,max:16,step:1,initial:1,when:p=>p.mode==='summon' },
    { key:'spacing',label:'布置半径',unit:'米',min:0,max:20,step:.1,initial:1,when:p=>p.mode==='summon' },
    { key:'maxAlive',label:'同来源存活上限',min:1,max:64,step:1,initial:4,when:p=>p.mode==='summon' }
  ] },
  'refill-ammo':{title:'补充弹药',color:'#657664',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'amount',label:'补充数量',min:0,max:1000000,step:1,initial:60},
    {key:'recipient',label:'补充对象',type:'select',initial:'self',options:[['self','效果来源'],['target','当前目标']]}
  ]},
  'teleport-projectile':{title:'传送到追踪弹',color:'#6e6988',inputs:['execute'],outputs:[['next','继续']],fields:[
    {key:'trackingKey',label:'弹体追踪键',type:'text',initial:'warp'}
  ]}
};
export function nodeOutputs(node: GraphNode): [string,string][] {
  const ports=kinds[node.kind]?.outputs??[];
  if(node.kind!=='delivery')return ports;
  const allowed=node.parameters.mode==='summon'?['apply','expire','removed']:node.parameters.mode==='ray'?['apply','wall','complete']:node.parameters.mode==='area'&&node.parameters.persistent===true?['apply','enter','exit','stay','expire']:['apply'];
  return ports.filter(([port])=>allowed.includes(port));
}
export const deliveryLabel = (node: GraphNode) => node.kind==='projectile'?'投射物':node.parameters.mode==='summon'?'召唤分身':node.parameters.mode==='area'?(node.parameters.persistent?'持续区域':'瞬时区域'):node.parameters.mode==='ray'?'射线判定':node.parameters.mode==='melee'?'近战判定':node.parameters.mode==='displacement'?(node.parameters.movementMode==='blink'?'闪现打击':'冲刺打击'):'目标作用';
export const portName = (port: string) => port === 'target' ? '目标数据' : '执行';
export const effectKinds:[string,string][]=[['damage','伤害结算'],['impact','受击与状态'],['heal','治疗'],['buff','施加 Buff'],['dispel','驱散 Buff'],['cue','表现事件'],['split','命中分裂'],['after-damage','扣血后条件'],['refill-ammo','补充弹药'],['teleport-projectile','传送到追踪弹']];
export const parameterValue = (field: Field, parameters: Parameters) => parameters[field.key] ?? (field.key === 'energyPerSecond' && parameters.mode === 'charge' ? 0 : field.initial);
export const clone = <T,>(value: T): T => structuredClone(value);
export async function api<T>(path: string, body?: unknown, method = 'POST'): Promise<T> {
  const response = await fetch('/api' + path, { method, headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
  if (response.status === 204) return undefined as T;
  const data = await response.json();
  if (!response.ok) throw new Error(data.error ?? data.detail ?? `请求失败 ${response.status}`);
  return data;
}
