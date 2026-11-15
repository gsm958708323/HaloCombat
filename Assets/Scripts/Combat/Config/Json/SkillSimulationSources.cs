using Combat.Core;

namespace Combat.Config
{
    public sealed partial class SkillSimulation
    {
        readonly AuthorExecutionTrace _trace;
        readonly System.Collections.Generic.Dictionary<long,AuthorNodeSource> _bodySources=new System.Collections.Generic.Dictionary<long,AuthorNodeSource>();
        readonly System.Collections.Generic.Dictionary<long,SkillOrigin> _bodyOrigins=new System.Collections.Generic.Dictionary<long,SkillOrigin>();
        AuthorNodeSource BodySource(EntityId id)=>_bodySources.TryGetValue(HitboxComp.Pack(id),out var source)?source:null;
        static string Packed(EntityId id)=>id.IsValid?HitboxComp.Pack(id).ToString():null;
        SkillOrigin OriginOf(EntityId id)
        {
            if(!_world.TryGetActor(id,out var a))return _bodyOrigins.TryGetValue(HitboxComp.Pack(id),out var origin)?origin:default;
            if(a.TryGetComp<ProjectileComp>(out var p))return p.Family.Origin;
            if(a.TryGetComp<AoeComp>(out var area))return area.Origin;
            if(a.TryGetComp<ConstructComp>(out var c))return c.Origin;
            return a.TryGetComp<SkillDirectorComp>(out var d)?d.CurrentOrigin:default;
        }
        AuthorNodeSource ActorSource(Actor a)
        {
            if(a==null)return null;
            if(a.TryGetComp<ProjectileComp>(out var p)&&p.Def!=null)return _content.Sources.Definition("projectile",p.Def.SpecId);
            if(a.TryGetComp<AoeComp>(out var area)&&area.Def!=null)return _content.Sources.Definition("area",area.Def.SpecId);
            if(a.TryGetComp<ConstructComp>(out var c)&&c.Def!=null)return _content.Sources.Definition("construct",c.Def.SpecId);
            if(a.TryGetComp<SummonComp>(out var s)&&s.Def!=null)return _content.Sources.Definition("summon",s.Def.SpecId);
            return a.TryGetComp<SkillDirectorComp>(out var d)&&d.IsPlaying?_content.Sources.Definition("skill",d.CurrentSkill.Value):null;
        }
    }
}
