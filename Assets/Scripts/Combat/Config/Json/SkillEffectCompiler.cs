using System;
using System.Collections.Generic;
using Combat.Core;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    internal sealed class SkillEffectCompiler
    {
        readonly EffectGraphIndex _graph;
        readonly CombatContentSnapshot _content;
        readonly HashSet<int> _projectiles, _aoes,_summons;
        readonly Dictionary<string, IEffect[]> _compiled = new Dictionary<string, IEffect[]>();
        readonly bool _requiresTarget;
        public List<TimelineClip> Clips { get; } = new List<TimelineClip>();
        public List<TimelinePayload> Followups { get; } = new List<TimelinePayload>();
        public float AimRange { get; private set; }
        public SkillEffectCompiler(EffectGraphIndex graph, CombatContentSnapshot content, HashSet<int> projectiles, HashSet<int> aoes,HashSet<int> summons)
        { _graph = graph; _content = content; _projectiles = projectiles; _aoes = aoes;_summons=summons;
            string mode = graph.EffectOnly?null:graph.Root("target").Parameters.Value<string>("mode");
            _requiresTarget = mode == "entity" || mode == "nearest" || mode == "random"; }

        public IEffect[] Outputs(SkillNodeDocument node, string port, bool allowSpatial = false)
        {
            var effects = new List<IEffect>();
            foreach (var next in _graph.Outputs(node, port, "execute")) effects.AddRange(Compile(next,allowSpatial));
            return effects.ToArray();
        }
        public IEffect[] Entries(string[] ids,string path)
        {
            if(ids==null||ids.Length>256)throw new ContentValidationException(path,"Expected at most256 effect entry identities.");
            var result=new List<IEffect>();
            for(int i=0;i<ids.Length;i++)result.AddRange(Compile(_graph.Entry(ids[i],path+"["+i+"]"),false));
            return result.ToArray();
        }

        IEffect[] Compile(SkillNodeDocument node,bool allowSpatial)
        {
            if (!node.Enabled) { _graph.IgnoreBranch(node); return Array.Empty<IEffect>(); }
            if(node.Kind=="delivery"&&(node.Parameters?.Value<string>("mode")=="melee"||node.Parameters?.Value<string>("mode")=="displacement"))
            {
                if(!allowSpatial)throw new ContentValidationException(_graph.NodePath(node),"Actor actions must be scheduled from the cast output, not an effect event.");
                Spatial(node);return Array.Empty<IEffect>();
            }
            if (_compiled.TryGetValue(node.Id, out var existing)) return existing;
            int clipCount=Clips.Count,followupCount=Followups.Count;
            string path = _graph.NodePath(node);
            var effects = new List<IEffect>();
            switch (node.Kind)
            {
                case "construct":
                    effects.Add(ConstructNodeCompiler.Compile(node,path,_content,port=>Outputs(node,port)));break;
                case "construct-chain":
                    var chainObjects=new NodeParameters(node,path,"group","radius","damage");
                    effects.Add(new ChainConstructsEffect(chainObjects.Text("group","barrel"),chainObjects.Number("radius",2.2f,.01f,100),chainObjects.Number("damage",9999,0,10000000)));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "projectile":
                    effects.Add(Projectile(node,path));break;
                case "projectile-ref":
                    var reference=new NodeParameters(node,path,"projectile","count","spread");string projectileKey=reference.Text("projectile");
                    if(!_content.SharedProjectiles.TryGetValue(projectileKey,out int spec))throw new ContentValidationException(path+".parameters.projectile","Unknown shared projectile.");
                    _content.Dependencies.Add(_graph.ContentKey,ContentDependencies.Projectile(projectileKey));
                    effects.Add(new SpawnProjectileEffect(spec,reference.Integer("count",1,1,64),reference.Number("spread",0,0,360)));break;
                case "area-ref":
                    var areaRef=new NodeParameters(node,path,"area","center","forwardOffset");string areaKey=areaRef.Text("area");
                    if(!_content.SharedAreas.TryGetValue(areaKey,out int areaSpec))throw new ContentValidationException(path+".parameters.area","Unknown shared area.");
                    _content.Dependencies.Add(_graph.ContentKey,ContentDependencies.Area(areaKey));
                    effects.Add(new SpawnAoeEffect(areaSpec,Center(areaRef),areaRef.Number("forwardOffset",0,0,100)));break;
                case "split":
                    var split=new NodeParameters(node,path,"count","spread","generations","powerMultiplier","rangeMode","range");
                    effects.Add(new SplitProjectileEffect(split.Integer("count",3,1,32),split.Number("spread",60,0,360),
                        split.Integer("generations",1,0,8),split.Number("powerMultiplier",.5f,.001f,10),
                        split.Choice("rangeMode","inherit","inherit","fixed")=="inherit",split.Number("range",8,.01f,1000)));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "delivery": effects.Add(Delivery(node, path)); break;
                case "damage":
                    var d = new NodeParameters(node, path, "flat", "coefficient", "critChance", "critMultiplier", "ignoreDefense", "hitstopFrames", "scope", "center", "radius", "maxTargets","scaleByStacks","notifyHurt","damageType","includeBonuses","critFromAttribute","snapshotAttack","canCrit","sourceHitstopFrames","targetHitstopFrames","feedback","feedbackHitstop");
                    IEffect damage = new DamageEffect
                    {
                        Flat = d.Number("flat", 80, 0, 10000000), Coeff = d.Number("coefficient", 0, 0, 100000), CanCrit = d.Boolean("canCrit",true),
                        CritMul = d.Number("critMultiplier", 1.8f, 1, 100), CritChance = d.Number("critChance", .15f, 0, 1),
                        HitstopFrames = d.Integer("hitstopFrames", 0, 0, 1000), IgnoreDef = d.Boolean("ignoreDefense", false),
                        SourceHitstopFrames=d.Integer("sourceHitstopFrames",0,0,1000),TargetHitstopFrames=d.Integer("targetHitstopFrames",0,0,1000),
                        Feedback=Feedback(d),
                        FeedbackHitstop=d.Boolean("feedbackHitstop",false),
                        InheritSkillFeedback=d.Choice("feedback","skill","skill","none","light","confirm","heavy","finisher")=="skill",
                        ScaleByBuffStacks=d.Boolean("scaleByStacks",false),FireOnHurted=d.Boolean("notifyHurt",true),UseSnapshotAtk=d.Boolean("snapshotAttack",true),
                        Kind=d.Choice("damageType","physical","physical","elemental")=="physical"?DamageKind.Physical:DamageKind.Elemental,IncludeBonuses=d.Boolean("includeBonuses",true)
                    };
                    if(d.Boolean("critFromAttribute",false))((DamageEffect)damage).CritChance=-1;
                    if (d.Choice("scope", "target", "target", "area") == "area")
                        damage = new CircleEffects(new[] { damage }, Center(d), d.Number("radius", 3, .01f, 100), d.Integer("maxTargets", 0, 0, 128));
                    effects.Add(damage); effects.AddRange(Outputs(node, "next",allowSpatial));
                    break;
                case "cue":
                    var cue = new NodeParameters(node, path, "id","name","center","playback","instanceKey","stopWithCast","anchor");
                    _content.RequiredCues.Add(cue.Integer("id",1,1,1000000));
                    string cueCenter=cue.Choice("center","source","source","point","target");
                    string playback=cue.Choice("playback","once","once","start","stop"),instanceKey=cue.OptionalIdentity("instanceKey");
                    if(playback!="once"&&(string.IsNullOrEmpty(instanceKey)||cueCenter=="point"))throw new ContentValidationException(path,"Keyed Cue needs an entity center and instanceKey.");
                    if(playback=="once"&&!string.IsNullOrEmpty(instanceKey))throw new ContentValidationException(path+".parameters.instanceKey","One-shot Cue has no instance key.");
                    int cueId=cue.Integer("id",1,1,1000000);string cueName=cue.Text("name","Skill");
                    bool stopWithCast=cue.Boolean("stopWithCast",true);
                    string anchor=cue.OptionalIdentity("anchor");if(anchor.Length>0&&cueCenter=="point")throw new ContentValidationException(path+".parameters.anchor","Anchors need an entity center.");
                    effects.Add(playback!="once"||anchor.Length>0?new CuePlaybackEffect(cueId,cueName,cueCenter=="target"?EffectCenter.Target:EffectCenter.Source,(CuePlayback)Array.IndexOf(new[]{"once","start","stop"},playback),instanceKey,stopWithCast,anchor):
                        cueCenter=="source"?(IEffect)new PlayCueEffect(cueId,cueName):new PlayPositionedCueEffect(cueId,cueName,cueCenter=="point"?EffectCenter.Point:EffectCenter.Target));
                    effects.AddRange(Outputs(node, "next",allowSpatial));
                    break;
                case "buff":
                    var b=new NodeParameters(node,path,"buff","stacks","recipient");var definition=Buff(b.Text("buff"),path+".parameters.buff");
                    effects.Add(new TargetEffects(new IEffect[]{new ApplyDurationEffect(definition,b.Integer("stacks",1,1,128))},b.Choice("recipient","target","target","self")=="self"));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "dispel":
                    var dispel=new NodeParameters(node,path,"mode","buff","group","tag","maxCount","recipient");
                    string filter=dispel.Choice("mode","buff","buff","group","tag","source");
                    int key=filter=="buff"?Buff(dispel.Text("buff"),path+".parameters.buff").BuffId:filter=="group"?dispel.Integer("group",1,1,1000000):0;
                    var mode=filter=="buff"?DispelMode.ByBuffId:filter=="group"?DispelMode.ByMutexGroup:filter=="tag"?DispelMode.ByTag:DispelMode.BySource;
                    effects.Add(new TargetEffects(new IEffect[]{new DispelEffect(mode,key,new TagId(dispel.Integer("tag",filter=="tag"?1:0,filter=="tag"?1:0,1000000)),dispel.Integer("maxCount",0,0,128))},dispel.Choice("recipient","target","target","self")=="self"));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "heal":
                    var heal=new NodeParameters(node,path,"amount");effects.Add(new HealEffect {Amount=heal.Number("amount",10,0,10000000)});
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "hurt-feedback":
                    var hurt=new NodeParameters(node,path,"level","requireHpDamage");
                    string grade=hurt.Choice("level","light","none","light","confirm","heavy","finisher","damage");
                    var level=grade=="damage"?HitFeedbackLevel.None:(HitFeedbackLevel)Array.IndexOf(new[]{"none","light","confirm","heavy","finisher"},grade);
                    effects.Add(new HurtFeedbackEffect(level,hurt.Boolean("requireHpDamage",true),grade=="damage"));effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "after-damage":
                    var after=new NodeParameters(node,path,"latestCastOnly");effects.Add(new AfterDamageEffect(Outputs(node,"apply"),after.Boolean("latestCastOnly",false)));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "impact":
                    effects.Add(Impact(node,path));effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "refill-ammo":
                    var ammo=new NodeParameters(node,path,"amount","recipient");
                    effects.Add(new TargetEffects(new IEffect[]{new RefillAmmoEffect(ammo.Integer("amount",60,0,1000000))},ammo.Choice("recipient","self","self","target")=="self"));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                case "teleport-projectile":
                    var teleport=new NodeParameters(node,path,"trackingKey");effects.Add(new TeleportTrackedProjectileEffect(teleport.Text("trackingKey")));
                    effects.AddRange(Outputs(node,"next",allowSpatial));break;
                default: throw new ContentValidationException(path + ".kind", "Unsupported effect kind: " + node.Kind);
            }
            var result=effects.ToArray();
            if(clipCount==Clips.Count&&followupCount==Followups.Count)_compiled[node.Id]=result;
            return result;
        }

        IEffect Impact(SkillNodeDocument node,string path)
        {
            var p=new NodeParameters(node,path,"mode","recipient","duration","iframe","distance","speed","center","amount");
            string mode=p.Choice("mode","hitstun","hitstun","knockback","knockdown","iframe","launch","pull","shield");
            float duration=p.Number("duration",.3f,.001f,120),frames=p.Number("iframe",0,0,120),distance=p.Number("distance",2,0,100),speed=p.Number("speed",6,.01f,100),amount=p.Number("amount",100,0,10000000);
            IEffect effect=mode=="hitstun"?new HitStunEffect{Duration=duration,IFrameDuration=frames}:
                mode=="knockback"?(IEffect)new KnockbackEffect{Distance=distance}:mode=="knockdown"?new KnockdownEffect{Duration=duration}:
                mode=="iframe"?new IFrameEffect{Duration=duration}:mode=="launch"?new LaunchEffect(speed):mode=="pull"?new PullTowardPointEffect(distance,
                    (EffectCenter)Array.IndexOf(new[]{"point","target","source"},p.Choice("center","source","point","target","source"))):new AddShieldEffect(amount);
            return new TargetEffects(new[]{effect},p.Choice("recipient","target","target","self")=="self");
        }

        IEffect Projectile(SkillNodeDocument node,string path)
        {
            var p=new NodeParameters(node,path,"speed","range","lifetime","radius","maxHits","homingRate",
                "multi","count","spread","chain","chainCount","chainRadius","chainMode","homingRetarget","homingRadius",
                "trajectory","acceleration","maxSpeed","returnAfter","returnSpeed","catchRadius","launchHeight","verticalSpeed","gravity","restitution","groundBounces","wallBounces","spawnForward","snapshotAttack","hostileMask","cueId","homingMaxTurn","trackingKey","rampTime","sameTargetDelay","passInvulnerable","returnCurve","groundPhases","bounceHeight","removeOnObstacle","view");
            CheckId(node,path,_projectiles,"projectile");
            int projectileCue=p.Integer("cueId",0,0,1000000);if(projectileCue>0)_content.RequiredCues.Add(projectileCue);
            int maxHits=p.Integer("maxHits",1,0,1000000);
            int chainCount=p.Integer("chainCount",2,1,64);
            float chainRadius=p.Number("chainRadius",6,.01f,1000);
            var chainMode=p.Choice("chainMode","nearest","nearest","random");
            var chain=p.Boolean("chain",false)?new ProjectileChainDefinition {ExtraChains=chainCount,Radius=chainRadius,Mode=chainMode=="random"?ChainTargetMode.Random:ChainTargetMode.Nearest}:null;
            if(chain!=null&&maxHits==0)throw new ContentValidationException(path+".parameters.maxHits","Chain needs a finite per-segment hit budget; unlimited piercing has no segment endpoint.");
            float speed=p.Number("speed",12,.1f,1000);
            string trajectory=p.Choice("trajectory","linear","linear","accelerate","return","bounce","ramp","phase-bounce");
            float acceleration=p.Number("acceleration",6,-1000,1000),maxSpeed=p.Number("maxSpeed",60,.1f,1000);
            float returnAfter=p.Number("returnAfter",.6f,0,3600),returnSpeed=p.Number("returnSpeed",12,.1f,1000),catchRadius=p.Number("catchRadius",.4f,.01f,10);
            var bounce=new ProjectileBounceDefinition {LaunchHeight=p.Number("launchHeight",.5f,0,100),VerticalSpeed=p.Number("verticalSpeed",5,0,100),
                Gravity=p.Number("gravity",20,.1f,1000),Restitution=p.Number("restitution",.6f,0,1),Count=p.Integer("groundBounces",2,0,32)};
            int walls=p.Integer("wallBounces",0,0,64);
            bool sine=p.Choice("returnCurve","constant","constant","sine")=="sine";
            if(trajectory=="return"&&sine&&returnAfter<=0)throw new ContentValidationException(path+".parameters.returnAfter","Sine return requires a positive turn time.");
            var phases=trajectory=="phase-bounce"?new ScheduledBounceDefinition{Height=p.Number("bounceHeight",1,0,100),Phases=p.Times("groundPhases",new[]{1f,1.6667f,1.999f})}:null;
            if(trajectory=="accelerate"&&maxSpeed<speed)throw new ContentValidationException(path+".parameters.maxSpeed","Maximum speed cannot be less than initial speed.");
            var motion=trajectory=="linear"&&walls==0?null:new ProjectileMotionDefinition {Acceleration=trajectory=="accelerate"?acceleration:0,MaxSpeed=trajectory=="accelerate"?maxSpeed:1000,
                ReturnAfter=trajectory=="return"?returnAfter:-1,ReturnSpeed=returnSpeed,CatchRadius=catchRadius,Bounce=trajectory=="bounce"?bounce:null,SineReturn=trajectory=="return"&&sine,ScheduledBounce=phases,WallBounces=walls,RampTime=trajectory=="ramp"?p.Number("rampTime",5,.001f,3600):0};
            _content.Projectiles.Register(new ProjectileDefinition
            {
                SpecId=node.RuntimeId,Speed=speed,Range=p.Number("range",24,0,1000),Lifetime=p.Number("lifetime",120,.001f,3600),Motion=motion,TrackingKey=p.OptionalIdentity("trackingKey"),
                SpawnForward=p.Number("spawnForward",.4f,0,100),SnapshotAtk=p.Boolean("snapshotAttack",true),HostileMask=p.Integer("hostileMask",0,0,int.MaxValue),CueId=projectileCue,HomingMaxTurn=p.Number("homingMaxTurn",0,0,10000),
                HitRadius=p.Number("radius",.18f,.01f,100),MaxHits=maxHits,Chain=chain,
                SameTargetDelay=p.Number("sameTargetDelay",0,0,3600),PassInvulnerable=p.Boolean("passInvulnerable",false),
                RemoveOnObstacle=p.Boolean("removeOnObstacle",true),
                View=BodyView(p),
                HomingRate=p.Number("homingRate",0,0,36000),HomingRetarget=p.Boolean("homingRetarget",false),HomingAcquireRadius=p.Number("homingRadius",12,.01f,1000),
                OnHit=Outputs(node,"hit"),OnWall=Outputs(node,"wall"),OnRange=Outputs(node,"range"),OnExpire=Outputs(node,"lifetime"),OnEnd=Outputs(node,"expire"),
                OnBounce=Outputs(node,"bounce"),OnReturned=Outputs(node,"returned"),OnGround=Outputs(node,"ground")
            });
            int count=p.Integer("count",3,1,64);float spread=p.Number("spread",45,0,360);
            bool multiple=p.Boolean("multi",false);
            return new SpawnProjectileEffect(node.RuntimeId,multiple?count:1,multiple?spread:0);
        }

        IEffect Delivery(SkillNodeDocument node, string path)
        {
            var p = new NodeParameters(node, path, "mode", "recipient", "center", "radius", "maxTargets", "persistent", "duration", "interval", "pulseOnSpawn",
                "shape","length","width","angle","startup","activeDuration","movementMode","distance","speed","hitRadius","pierce","blueprint","count","spacing","maxAlive","permanent","cueId","hostileMask",
                "areaMotion","moveSpeed","removeOnObstacle","absorbProjectiles","absorbForce","radiusPerProjectile","forwardOffset","view");
            string mode = p.Choice("mode", "target", "target", "area","ray","summon");
            if(mode=="summon")
            {
                _graph.RequireNoOutputs(node,"enter","exit","stay","wall","complete","absorb");
                string key=p.Text("blueprint");
                if(!_content.Blueprints.TryGet(key,out var blueprint))throw new ContentValidationException(path+".parameters.blueprint","Unknown summon blueprint: "+key);
                CheckId(node,path,_summons,"summon");
                _content.Dependencies.Add(_graph.ContentKey,ContentDependencies.Skill(blueprint.SkillKey));
                _content.Summons.Register(new SummonDefinition
                {
                    SpecId=node.RuntimeId,BlueprintId=key,OriginContentKey=_graph.ContentKey,Count=p.Integer("count",1,1,16),Spacing=p.Number("spacing",1,0,20),
                    Lifetime=p.Boolean("permanent",false)?0:p.Number("duration",3,.02f,120),MaxAlive=p.Integer("maxAlive",4,1,64),CollisionRadius=blueprint.CollisionRadius,
                    AcquireRadius=blueprint.AcquireRadius,FollowRange=blueprint.FollowRange,
                    OnSpawn=Outputs(node,"apply"),OnExpire=Outputs(node,"expire"),OnRemoved=Outputs(node,"removed")
                });
                return new SpawnSummonEffect(node.RuntimeId,Center(p));
            }
            _graph.RequireNoOutputs(node,"removed");
            if(mode=="ray")
            {
                _graph.RequireNoOutputs(node,"enter","exit","stay","expire","absorb");
                return new RayStrikeEffect(Outputs(node,"apply"),Outputs(node,"wall"),Outputs(node,"complete"),
                    p.Number("length",20,.01f,1000),p.Number("width",.3f,.01f,100),p.Boolean("pierce",false),p.Integer("maxTargets",0,0,128));
            }
            _graph.RequireNoOutputs(node,"wall","complete");
            bool persistent = mode == "area" && p.Boolean("persistent", false);
            if (!persistent) _graph.RequireNoOutputs(node, "enter", "exit", "stay", "expire","absorb");
            var apply = Outputs(node, "apply");
            if (!persistent && apply.Length == 0) throw new ContentValidationException(path, "Delivery needs an enabled apply effect.");
            if (mode == "target") return new TargetEffects(apply, p.Choice("recipient", "target", "target", "self") == "self");
            var center = Center(p); float radius = p.Number("radius", 3, .01f, 100);
            int maximum = p.Integer("maxTargets", 0, 0, 128);
            if (!persistent) return new CircleEffects(apply, center, radius, maximum,p.Integer("hostileMask",0,0,int.MaxValue));
            CheckId(node, path, _aoes, "area");
            int areaCue=p.Integer("cueId",0,0,1000000);if(areaCue>0)_content.RequiredCues.Add(areaCue);
            var enter = Outputs(node, "enter"); var exit = Outputs(node, "exit"); var stay = Outputs(node, "stay");
            var expire = Outputs(node, "expire");
            bool absorb=p.Boolean("absorbProjectiles",false);
            if(!absorb)_graph.RequireNoOutputs(node,"absorb");
            var absorbed=Outputs(node,"absorb");
            if (apply.Length + enter.Length + exit.Length + stay.Length + expire.Length == 0&&!absorb)
                throw new ContentValidationException(path, "Persistent area needs a lifecycle effect.");
            _content.Aoes.Register(new AoeDefinition
            {
                SpecId = node.RuntimeId, Radius = radius, Duration = p.Boolean("permanent",false)?0:p.Number("duration", 2, .02f, 120),
                PulseInterval = p.Number("interval", .5f, 0, 60), PulseOnSpawn = p.Boolean("pulseOnSpawn", true), MaxTargets = maximum,
                CueId=areaCue,HostileMask=p.Integer("hostileMask",0,0,int.MaxValue),
                View=BodyView(p),
                TrackOccupancy = enter.Length + exit.Length + stay.Length > 0,
                MoveForward=p.Choice("areaMotion","static","static","forward")=="forward",MoveSpeed=p.Number("moveSpeed",0,0,1000),RemoveOnObstacle=p.Boolean("removeOnObstacle",true),
                AbsorbProjectiles=absorb,ProjectileAbsorbForce=absorb?p.Number("absorbForce",.05f,0,10):0,ProjectileRadiusScale=absorb?p.Number("radiusPerProjectile",.05f,0,10):0,OnAbsorb=absorbed,
                OnPulse = apply, OnEnter = enter, OnExit = exit, OnStay = stay, OnExpire = expire
            });
            return new SpawnAoeEffect(node.RuntimeId, center,p.Number("forwardOffset",0,0,100));
        }

        void Spatial(SkillNodeDocument node)
        {
            string path=_graph.NodePath(node);
            var p=new NodeParameters(node,path,"mode","recipient","center","radius","maxTargets","persistent","duration","interval","pulseOnSpawn",
                "shape","length","width","angle","startup","activeDuration","movementMode","distance","speed","hitRadius","pierce","blueprint","count","spacing","maxAlive");
            string mode=p.Choice("mode","melee","melee","displacement");
            _graph.RequireNoOutputs(node,"enter","exit","stay","expire","wall","complete","removed","absorb");
            var effects=Outputs(node,"apply");
            if(effects.Length==0)throw new ContentValidationException(path,"Action needs an enabled apply effect.");
            float start=p.Number("startup",.1f,0,60);int maximum=p.Integer("maxTargets",0,0,128);
            if(mode=="melee")
            {
                string shape=p.Choice("shape","thrust","thrust","fan","circle");
                Clips.Add(new TimelineClip {Kind=ClipKind.Melee,Start=start,End=start+p.Number("activeDuration",.2f,.001f,60),
                    Shape=(HitShape)Array.IndexOf(new[]{"thrust","fan","circle"},shape),Length=p.Number("length",3,.01f,100),
                    Width=p.Number("width",1,.01f,100),Angle=p.Number("angle",90,1,360),MaxTargets=maximum,OnHit=effects});
            }
            else
            {
                float distance=p.Number("distance",6,.01f,100);float radius=p.Number("hitRadius",.5f,.01f,20);
                string movement=p.Choice("movementMode","dash","dash","blink");
                if(movement=="dash")Clips.Add(new TimelineClip {Kind=ClipKind.Dash,Start=start,End=start+distance/p.Number("speed",12,.1f,1000),
                    Length=distance,Width=radius*2,MaxTargets=maximum,OnHit=effects});
                else
                {
                    AimRange=AimRange>0?Math.Min(AimRange,distance):distance;
                    Followups.Add(new TimelinePayload {Time=start,Effects=new IEffect[]{new BlinkStrikeEffect(effects,distance,radius,maximum,_requiresTarget)}});
                }
            }
        }

        static EffectCenter Center(NodeParameters parameters)
            => (EffectCenter)Array.IndexOf(new[] { "point", "target", "source","auto" }, parameters.Choice("center", "point", "point", "target", "source","auto"));
        string BodyView(NodeParameters parameters)
        {string key=parameters.OptionalIdentity("view");if(key.Length==0)return null;_content.RequiredBodyViews.Add(key);return key;}
        static HitFeedbackLevel Feedback(NodeParameters p)
        {string value=p.Choice("feedback","skill","skill","none","light","confirm","heavy","finisher");return value=="skill"?HitFeedbackLevel.None:(HitFeedbackLevel)Array.IndexOf(new[]{"none","light","confirm","heavy","finisher"},value);}
        DurationSpec Buff(string id,string path)
        {
            if(!_content.Buffs.TryGet(id,out var buff))throw new ContentValidationException(path,"Unknown Buff identity: "+id);
            _content.Dependencies.Add(_graph.ContentKey,ContentDependencies.Buff(id));return buff;
        }
        static void CheckId(SkillNodeDocument node, string path, HashSet<int> ids, string type)
        {
            if (node.RuntimeId <= 0 || !ids.Add(node.RuntimeId)) throw new ContentValidationException(path + ".runtimeId", "Expected unique positive " + type + " definition id.");
        }
    }
}
