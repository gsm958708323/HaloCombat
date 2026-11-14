using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    // A session owns gameplay state only; Unity binds presentation before calling Start.
    public sealed class ArenaSession : IDisposable
    {
        public const float Step=.02f;
        static readonly string[] Priority={"Roll","Fire4","Monkey","Homing","Fire5","Fire3","Fire2","Fire1"};
        readonly CombatContentSnapshot _content;
        readonly Dictionary<string,InputToken> _bindings;
        readonly Queue<InputToken> _presses=new Queue<InputToken>();
        readonly List<(EntityId id,double at)> _corpses=new List<(EntityId,double)>();
        readonly Dictionary<string,SkillNodeId> _slots;
        readonly Dictionary<string,int> _slotCasts=new Dictionary<string,int>();
        SimVec3 _trainingPosition;
        double _wallTime,_spawnAge;
        float _moveX,_moveZ;
        bool _started,_disposed,_first=true;
        public CombatWorld World {get;}
        public ArenaDefinition Definition {get;}
        public EntityId PlayerId {get;private set;}
        public bool PlayerDead {get;private set;}
        public bool Paused {get;private set;}
        public bool Training {get;}
        public int SpawnedEnemies {get;private set;}
        public int EnemyCount=>World.RegistryActive().Count(a=>a.Id!=PlayerId&&a.TryGetComp<HealthComp>(out _)&&a.TryGetComp<TeamComp>(out var t)&&t.TeamId==_content.Runtime.Actors[Definition.Enemy].Team&&!a.GetComp<TagComp>().Has(CommonTags.Dead)&&!a.TryGetComp<SummonComp>(out _));
        public IReadOnlyDictionary<string,SkillNodeId> Slots=>_slots;
        public ArenaSession(CombatContentSnapshot content,bool training=false,IReadOnlyDictionary<string,SkillNodeId> slots=null)
        {
            _content=content??throw new ArgumentNullException(nameof(content));Definition=content.Runtime?.Arena??throw new ContentValidationException("$.runtime.arena","Arena definition is required.");
            Training=training;_bindings=Definition.Bindings.ToDictionary(b=>b.Action,b=>b.Input,StringComparer.Ordinal);
            _slots=new Dictionary<string,SkillNodeId>();if(slots!=null)foreach(var slot in slots)_slots.Add(slot.Key,slot.Value);
            World=new CombatWorld(content.Runtime.CreateFactory(content.Skills),random:new SeededRandom(Definition.Seed),cues:content.Runtime.Data.Cues,
                motor:content.Runtime.Data.Motor,obstacles:Definition.Obstacles,skillEvents:content.SkillEvents);
            content.Runtime.Data.Install(World);World.Events.Subscribe<EvEntityDead>(OnDead);
        }
        public void Start()
        {
            if(_disposed||_started)throw new InvalidOperationException("Session cannot start twice.");_started=true;
            PlayerId=Spawn(Definition.Player,Definition.PlayerPosition,Definition.PlayerYaw);
            if(World.TryGetActor(PlayerId,out var player))player.GetComp<PlayerAimComp>().PrimaryToken=_bindings["Fire1"];
            if(Training)
            {
                var forward=LocomotionComp.ForwardFromYaw(Definition.PlayerYaw);
                _trainingPosition=new SimVec3(Definition.PlayerPosition.X+forward.X*4,World.Motor.GroundY,Definition.PlayerPosition.Z+forward.Z*4);
                float radius=_content.Runtime.Actors[Definition.TrainingTarget].CollisionRadius;
                if(!FreeInside(_trainingPosition,radius)&&!TryPosition(radius,out _trainingPosition))throw new InvalidOperationException("No free training target spawn.");
            }
        }
        EntityId Spawn(string blueprint,SimVec3 position,float yaw)
        {
            var id=World.SpawnActor(new ActorSpawnSpec(blueprint),false);World.TryGetActor(id,out var actor);
            var tf=actor.GetComp<TransformComp>();tf.Position=position;tf.YawDegrees=yaw;
            if(actor.TryGetComp<BehaviorTreeComp>(out var bt)){bt.Board.Target=PlayerId;bt.Board.Home=position;}
            World.PublishSpawn(id,blueprint);return id;
        }
        public void ApplyInput(in ArenaInputFrame frame)
        {
            if(_disposed||!_started||Paused||PlayerDead||!World.TryGetActor(PlayerId,out var player))return;
            if(!Finite(frame.MoveX)||!Finite(frame.MoveZ)||frame.AimValid&&(!Finite(frame.Aim.Point.X)||!Finite(frame.Aim.Point.Y)||!Finite(frame.Aim.Point.Z)||!Finite(frame.Aim.Yaw)))throw new ArgumentException("Input must be finite.");
            _moveX=Math.Max(-1,Math.Min(1,frame.MoveX));_moveZ=Math.Max(-1,Math.Min(1,frame.MoveZ));
            var state=player.GetComp<PlayerAimComp>();bool wasHeld=state.PrimaryHeld;state.PrimaryHeld=frame.PrimaryHeld;
            state.Valid=frame.AimValid;if(frame.AimValid)state.Aim=frame.Aim;
            if(wasHeld&&!frame.PrimaryHeld){ReleaseOwned(player,state.PrimaryCastId);state.PrimaryCastId=0;}
            int accepted=0;
            foreach(string action in Priority)
                if(frame.Pressed!=null&&Array.IndexOf(frame.Pressed,action)>=0&&_bindings.TryGetValue(action,out var token)&&accepted++<3)
                {if(_presses.Count==3)_presses.Dequeue();_presses.Enqueue(token);}
        }
        public bool CastSlot(string slot)
        {
            if(_disposed||Paused||PlayerDead||!_slots.TryGetValue(slot,out var skill)||!World.TryGetActor(PlayerId,out var player))return false;
            var tf=player.GetComp<TransformComp>();var aiming=player.GetComp<PlayerAimComp>();
            bool started=player.GetComp<SkillCastComp>().TryCast(skill,aiming.Valid?aiming.Aim:new SkillAim(EntityId.Invalid,tf.Position,tf.YawDegrees,false));
            if(started)_slotCasts[slot]=player.GetComp<SkillDirectorComp>().CurrentOrigin.CastId;
            return started;
        }
        public void ReleaseSlot(string slot)
        {if(!_slotCasts.TryGetValue(slot,out int castId))return;_slotCasts.Remove(slot);if(!_disposed&&World.TryGetActor(PlayerId,out var player))ReleaseOwned(player,castId);}
        static void ReleaseOwned(Actor player,int castId)
        {if(castId>0&&player.GetComp<SkillDirectorComp>().CurrentOrigin.CastId==castId)player.GetComp<SkillCastComp>().Release();}
        public void CancelInput()
        {
            _presses.Clear();_slotCasts.Clear();_moveX=_moveZ=0;
            if(_disposed||!World.TryGetActor(PlayerId,out var player))return;
            player.GetComp<InputBufferComp>().Clear();var aiming=player.GetComp<PlayerAimComp>();aiming.PrimaryHeld=false;aiming.Valid=false;aiming.PrimaryCastId=0;
            player.GetComp<LocomotionComp>().RequestMoveIntent(0,0);player.GetComp<SkillCastComp>().CancelHeld();
        }
        public void SetPaused(bool value){if(Paused==value)return;CancelInput();Paused=value;}
        public void Advance(int steps=1)
        {
            if(steps<0||steps>10000)throw new ArgumentOutOfRangeException(nameof(steps));
            if(_disposed||!_started||Paused||PlayerDead)return;
            for(int i=0;i<steps&&!PlayerDead;i++)
            {
                if(World.TryGetActor(PlayerId,out var player))
                {
                    var move=player.GetComp<LocomotionComp>();move.RequestMoveIntent(_moveX,_moveZ);
                    var aim=player.GetComp<PlayerAimComp>();if(aim.Valid)move.RequestAimYaw(aim.Aim.Yaw);
                    var input=player.GetComp<InputBufferComp>();if(!input.TryPeek(out _)&&_presses.Count>0)input.Push(_presses.Dequeue());
                }
                World.Tick(Step);if(PlayerDead)break;
                _spawnAge+=Step;
                if(_first||!Training&&_spawnAge+.000001>=Definition.SpawnPeriod)
                { _first=false;_spawnAge=0;if(Training)SpawnDummy();else FillEnemies(); }
            }
        }
        void SpawnDummy()
        {
            Spawn(Definition.TrainingTarget,_trainingPosition,Definition.PlayerYaw+180);
        }
        void FillEnemies()
        {
            int count=EnemyCount;float radius=_content.Runtime.Actors[Definition.Enemy].CollisionRadius;
            while(count<Definition.MaxEnemies&&TryPosition(radius,out var point))
            {Spawn(Definition.Enemy,point,90-World.Random.Next01()*360);SpawnedEnemies++;count++;}
        }
        bool TryPosition(float radius,out SimVec3 point)
        {
            World.TryGetActor(PlayerId,out var player);var origin=player.GetComp<TransformComp>().Position;
            for(int attempt=0;attempt<128;attempt++)
            {
                point=new SimVec3((World.Random.Next01()*2-1)*(Definition.HalfWidth-radius),World.Motor.GroundY,(World.Random.Next01()*2-1)*(Definition.HalfDepth-radius));
                if(SpatialGeometry.DistanceSquared(origin,point)<Definition.SpawnDistance*Definition.SpawnDistance||!FreeInside(point,radius))continue;
                var candidate=point;
                bool occupied=World.RegistryActive().Any(a=>a.TryGetComp<HealthComp>(out _)&&!a.GetComp<TagComp>().Has(CommonTags.Dead)&&SpatialGeometry.DistanceSquared(a.GetComp<TransformComp>().Position,candidate)<Math.Pow(radius+(a.TryGetComp<LocomotionComp>(out var loco)?loco.CollisionRadius:radius),2));
                if(!occupied)return true;
            }
            point=default;return false;
        }
        bool FreeInside(SimVec3 point,float radius)=>Math.Abs(point.X)+radius<=Definition.HalfWidth&&Math.Abs(point.Z)+radius<=Definition.HalfDepth&&World.Collision.IsFree(point,radius);
        void OnDead(EvEntityDead e)
        {_corpses.Add((e.Id,_wallTime+Definition.CleanupDelay));if(e.Id==PlayerId){PlayerDead=true;CancelInput();}}
        public void AdvanceWall(float dt)
        {
            if(_disposed||Paused)return;if(!Finite(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));_wallTime+=dt;
            for(int i=_corpses.Count-1;i>=0;i--)if(_wallTime+.000001>=_corpses[i].at){World.RequestDespawn(_corpses[i].id);_corpses.RemoveAt(i);}
            World.FlushCleanup();
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Dispose()
        {if(_disposed)return;CancelInput();_disposed=true;World.Events.Unsubscribe<EvEntityDead>(OnDead);World.Shutdown();_corpses.Clear();}
    }
}
