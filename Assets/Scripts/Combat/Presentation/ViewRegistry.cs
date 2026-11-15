using System.Collections.Generic;
using Combat.Core;

namespace Combat.Presentation
{
    public sealed class ViewRegistry
    {
        readonly Dictionary<int, IActorView> _map = new Dictionary<int, IActorView>(64);
        readonly IViewFactory _factory;

        public ViewRegistry(IViewFactory factory)
        {
            _factory = factory;
        }

        public void HandleSpawn(EvEntitySpawn e)
        {
            if (_map.ContainsKey(e.Id.Index))
                ReleaseIndex(e.Id.Index);
            var view = _factory != null ? _factory.Create(e.ViewKey??e.BlueprintId) : null;
            if (view == null) return;
            view.Bind(e.Id, e.BlueprintId);
            _map[e.Id.Index] = view;
        }

        public void HandleDead(EvEntityDead e)
        {
            if (_map.TryGetValue(e.Id.Index, out var view)&&view.Id==e.Id)
                view.OnDead(e);
        }

        public void HandleCleanup(EvEntityCleanup e){if(_map.TryGetValue(e.Id.Index,out var view)&&view.Id==e.Id)ReleaseIndex(e.Id.Index);}
        public void HandleHurt(EvHurt e){if(_map.TryGetValue(e.Target.Index,out var view)&&view.Id==e.Target)view.OnHurt(e);}

        public void SampleAll(CombatWorld world, float alpha, bool hitstop,bool paused=false)
        {
            if (world == null) return;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!_map.TryGetValue(actor.Id.Index, out var view)||view.Id!=actor.Id)
                    continue;
                var tf = actor.TryGetComp<TransformComp>(out var transform) ? transform : null;
                var tags = actor.TryGetComp<TagComp>(out var tagComp) ? tagComp : null;
                var sm = actor.TryGetComp<StateMachineComp>(out var stateMachine) ? stateMachine : null;
                var director = actor.TryGetComp<SkillDirectorComp>(out var skillDirector) ? skillDirector : null;
                actor.TryGetComp<LocomotionComp>(out var locomotion);
                actor.TryGetComp<AoeComp>(out var area);
                bool stopped=hitstop||world.IsActorStopped(actor);
                var sample = new PoseSample
                {
                    Id = actor.Id,
                    LogicPos = tf != null ? tf.Position : SimVec3.Zero,
                    YawDeg = tf != null ? tf.YawDegrees : 0f,
                    Grounded = tags != null && tags.Has(CommonTags.Grounded),
                    Activity = sm != null ? sm.Current : ActivityId.None,
                    Skill = director != null ? director.CurrentSkill : SkillNodeId.None,
                    InHitstop = stopped,
                    ActionTime=director!=null&&director.IsPlaying?director.TimelineTime:0,ActionDuration=director!=null&&director.IsPlaying?director.TimelineDuration:0,ActionRate=director?.ActionRate??1,
                    Animation=director?.Animation,CastId=director!=null&&director.IsPlaying?director.CurrentOrigin.CastId:0,ActorTime=(float)actor.Time.Time,
                    MoveIntent=locomotion?.MoveIntent??SimVec3.Zero,MoveBlocked=tags?.Has(CommonTags.BlockMove)??false,
                    PresentationPaused=paused,
                    BodyScale=area?.Def!=null&&area.Def.Radius>0?area.Radius/area.Def.Radius:1,
                    Alpha = stopped ? 1f : alpha
                };
                view.Sample(sample);
            }
        }

        void ReleaseIndex(int index)
        {
            if (!_map.TryGetValue(index, out var view)) return;
            view.Release();
            _map.Remove(index);
        }

        public void ReleaseAll()
        {
            foreach (var pair in _map)
                pair.Value.Release();
            _map.Clear();
        }
    }
}
