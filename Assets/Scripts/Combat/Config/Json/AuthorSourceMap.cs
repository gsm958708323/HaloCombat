using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Combat.Core;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public sealed class AuthorNodeSource
    {
        public string GraphKind {get;}
        public string GraphId {get;}
        public string GraphName {get;}
        public string NodeId {get;}
        public string NodeKind {get;}
        internal AuthorNodeSource(string key,string name,SkillNodeDocument node)
        {int colon=key.IndexOf(':');GraphKind=key.Substring(0,colon);GraphId=key.Substring(colon+1);GraphName=name;NodeId=node.Id;NodeKind=node.Kind;}
    }

    public sealed class AuthorSourceMap
    {
        sealed class EffectIdentity:IEqualityComparer<IEffect>
        {public bool Equals(IEffect a,IEffect b)=>ReferenceEquals(a,b);public int GetHashCode(IEffect effect)=>RuntimeHelpers.GetHashCode(effect);}
        readonly Dictionary<IEffect,AuthorNodeSource> _effects=new Dictionary<IEffect,AuthorNodeSource>(new EffectIdentity());
        readonly Dictionary<(string Graph,string Node),AuthorNodeSource> _nodes=new Dictionary<(string,string),AuthorNodeSource>();
        readonly Dictionary<string,AuthorNodeSource> _definitions=new Dictionary<string,AuthorNodeSource>();
        readonly Dictionary<int,AuthorNodeSource> _sites=new Dictionary<int,AuthorNodeSource>();
        readonly Dictionary<(string Graph,string Node),int> _siteIds=new Dictionary<(string,string),int>();
        internal AuthorSourceMap(SkillProjectDocument project)
        {
            foreach(var s in project.Skills){Add("skill:"+s.Id,s.Name,s.Nodes);Root("skill",s.RuntimeId,"skill:"+s.Id,s.Nodes,"cast");}
            foreach(var b in project.Buffs){Add("buff:"+b.Id,b.Name,b.Nodes);Root("buff",b.RuntimeId,"buff:"+b.Id,b.Nodes,"buff-events");}
            foreach(var t in project.Timelines)Add("timeline:"+t.Id,t.Name,t.Nodes);
            foreach(var p in project.Projectiles)Add("projectile:"+p.Id,p.Name,p.Nodes);
            foreach(var a in project.Areas)Add("area:"+a.Id,a.Name,a.Nodes);
        }
        void Root(string family,int id,string graph,SkillNodeDocument[] nodes,string kind)
        {var node=nodes.FirstOrDefault(n=>n.Enabled&&n.Kind==kind);if(node!=null)_definitions[family+":"+id]=Node(graph,node.Id);}
        void Add(string key,string name,SkillNodeDocument[] nodes)
        {
            foreach(var node in nodes.Where(n=>n.Enabled))
            {
                var site=new AuthorNodeSource(key,name,node);_nodes.Add((key,node.Id),site);
                int id=_sites.Count+1;_sites.Add(id,site);_siteIds.Add((key,node.Id),id);
                string mode=node.Parameters?["mode"]?.Type==JTokenType.String?(string)node.Parameters["mode"]:null;
                bool persistent=node.Parameters?["persistent"]?.Type==JTokenType.Boolean&&(bool)node.Parameters["persistent"];
                string family=node.Kind=="projectile"?"projectile":node.Kind=="construct"?"construct":node.Kind=="delivery"?
                    mode=="summon"?"summon":mode=="area"&&persistent?"area":null:null;
                if(family!=null)_definitions[family+":"+node.RuntimeId]=site;
            }
        }
        internal AuthorNodeSource Node(string graph,string node)=>_nodes.TryGetValue((graph,node),out var source)?source:null;
        internal int SiteId(string graph,string node)=>_siteIds[(graph,node)];
        public AuthorNodeSource Site(int id)=>_sites.TryGetValue(id,out var source)?source:null;
        internal void Register(string graph,SkillNodeDocument node,IEffect[] effects)
        {
            var source=Node(graph,node.Id);
            // Next branches have already registered their own effects while compiling.
            foreach(var effect in effects)if(effect!=null&&!_effects.ContainsKey(effect))_effects.Add(effect,source);
        }
        public AuthorNodeSource Definition(string family,int id)=>_definitions.TryGetValue(family+":"+id,out var source)?source:null;
        internal AuthorNodeSource Effect(IEffect effect)=>_effects.TryGetValue(effect,out var source)?source:null;
    }

    public readonly struct AuthorExecution
    {
        public readonly AuthorNodeSource Node;
        public readonly EntityId Source,Target;
        public readonly SkillOrigin Origin;
        public readonly int Id,Parent;
        internal AuthorExecution(AuthorNodeSource node,in EffectContext context,int id,int parent)
        {Node=node;Source=context.Source?.Id??EntityId.Invalid;Target=context.Target?.Id??EntityId.Invalid;Origin=context.Origin;Id=id;Parent=parent;}
    }

    public sealed class AuthorExecutionTrace:IEffectObserver
    {
        readonly AuthorSourceMap _map;
        readonly List<AuthorExecution> _stack=new List<AuthorExecution>();
        int _sequence;
        public AuthorExecution Current=>_stack.Count==0?default:_stack[_stack.Count-1];
        public event Action<AuthorExecution> Executing;
        public AuthorExecutionTrace(AuthorSourceMap map)=>_map=map;
        public int Enter(IEffect effect,in EffectContext context)
        {
            var node=_map?.Effect(effect);if(node==null)return -1;
            int token=_stack.Count;var value=new AuthorExecution(node,context,++_sequence,Current.Id);_stack.Add(value);
            try{Executing?.Invoke(value);}catch{Exit(token);throw;}return token;
        }
        public void Exit(int token){if(token>=0&&token<_stack.Count)_stack.RemoveRange(token,_stack.Count-token);}
    }
}
