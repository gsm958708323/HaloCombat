using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    internal sealed class EffectGraphIndex
    {
        public readonly string ContentKey;
        public readonly SkillNodeDocument[] Nodes;
        public readonly SkillEdgeDocument[] Edges;
        public readonly bool BuffGraph;
        public readonly string Path;
        readonly Dictionary<string, SkillNodeDocument> _nodes = new Dictionary<string, SkillNodeDocument>();
        readonly HashSet<string> _used = new HashSet<string>();
        readonly List<SkillNodeDocument> _entries=new List<SkillNodeDocument>();
        static readonly HashSet<string> Kinds=new HashSet<string>{"target","cast","projectile","split","damage","cue","delivery","buff","dispel","heal","buff-events","skill-events","after-damage","impact","refill-ammo","teleport-projectile","construct","construct-chain"};
        internal static bool KnownKind(string kind)=>kind!=null&&Kinds.Contains(kind);
        public EffectGraphIndex(string contentKey,SkillNodeDocument[] nodes,SkillEdgeDocument[] edges, string path,bool buffGraph=false)
        {
            ContentKey=contentKey;Nodes=nodes;Edges=edges; Path=path;BuffGraph=buffGraph;
            if (nodes == null || edges == null) Fail(path, "Nodes and edges arrays are required.");
            if (nodes.Length > 256 || edges.Length > 1024) Fail(path, "Graph exceeds supported size.");
            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id)) Fail(path + ".nodes", "Every node needs an id.");
                if (_nodes.ContainsKey(node.Id)) Fail(NodePath(node), "Duplicate node id.");
                if (!Kinds.Contains(node.Kind))
                    Fail(NodePath(node) + ".kind", "Unknown node kind: " + node.Kind);
                if(buffGraph?(node.Kind=="target"||node.Kind=="cast"||node.Kind=="skill-events"):node.Kind=="buff-events")Fail(NodePath(node)+".kind","Root kind is invalid for this graph.");
                if (node.Position == null || !Finite(node.Position.X) || !Finite(node.Position.Y)) Fail(NodePath(node), "Invalid position.");
                _nodes.Add(node.Id, node);
            }
            var ids = new HashSet<string>();
            foreach (var edge in edges)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.Id) || !ids.Add(edge.Id)) Fail(path + ".edges", "Missing or duplicate edge id.");
                if (!_nodes.ContainsKey(edge.Source ?? "") || !_nodes.ContainsKey(edge.Target ?? "")) Fail(path + ".edges." + edge.Id, "Unknown endpoint.");
                if (edge.Order < 0) Fail(path + ".edges." + edge.Id, "Order cannot be negative.");
            }
            var finished = new HashSet<string>();
            foreach (var node in nodes) CheckCycle(node.Id, new HashSet<string>(), finished);
        }
        public string NodePath(SkillNodeDocument node) => Path + ".nodes[" + node.Id + "]";
        public SkillNodeDocument Entry(string id,string path)
        {
            if(!_nodes.TryGetValue(id??"",out var node))throw new ContentValidationException(path,"Unknown effect entry node.");
            if(node.Kind=="target"||node.Kind=="cast"||node.Kind=="skill-events"||node.Kind=="buff-events")throw new ContentValidationException(path,"Root is not an effect entry.");
            Use(node);_entries.Add(node);return node;
        }
        public SkillNodeDocument Root(string kind)
        {
            SkillNodeDocument result = null;
            foreach (var node in Nodes)
                if (node.Kind == kind && node.Enabled)
                {
                    if (result != null) Fail(NodePath(node), "Exactly one enabled " + kind + " node is required.");
                    result = node;
                }
            if (result == null) Fail(Path, "Missing " + kind + " node.");
            Use(result); return result;
        }
        public SkillNodeDocument OptionalRoot(string kind)
        {
            SkillNodeDocument result=null;
            foreach(var node in Nodes)if(node.Kind==kind&&node.Enabled)
            {if(result!=null)Fail(NodePath(node),"Only one enabled "+kind+" root is allowed.");result=node;}
            if(result!=null)Use(result);return result;
        }
        public List<SkillNodeDocument> Outputs(SkillNodeDocument source, string port, string targetPort)
        {
            var edges = new List<SkillEdgeDocument>(); var orders = new HashSet<int>();
            foreach (var edge in Edges)
                if (edge.Source == source.Id && edge.SourcePort == port)
                {
                    if (edge.TargetPort != targetPort) Fail(Path + ".edges." + edge.Id, "Expected target port " + targetPort);
                    if (!orders.Add(edge.Order)) Fail(Path + ".edges." + edge.Id, "Ambiguous output order.");
                    edges.Add(edge);
                }
            edges.Sort((a,b) => a.Order.CompareTo(b.Order));
            var result = new List<SkillNodeDocument>();
            foreach (var edge in edges) { var node = _nodes[edge.Target]; Use(node); result.Add(node); }
            return result;
        }
        public void CheckComplete()
        {
            foreach (var node in Nodes)
                if (node.Enabled && !_used.Contains(node.Id)) Fail(NodePath(node), "Enabled node is disconnected or unsupported.");
            foreach (var edge in Edges)
            {
                var kind = _nodes[edge.Source].Kind;
                bool valid = (kind == "target" && edge.SourcePort == "target" && edge.TargetPort == "target")
                    || (kind == "cast" && edge.SourcePort == "cast" && edge.TargetPort == "execute")
                    || (kind == "projectile" && Array.IndexOf(new[]{"hit","wall","range","lifetime","expire","bounce","returned","ground"},edge.SourcePort)>=0 && edge.TargetPort == "execute")
                    || (kind == "delivery" && Array.IndexOf(new[] { "apply", "enter", "exit", "stay", "expire","wall","complete","removed" }, edge.SourcePort) >= 0 && edge.TargetPort == "execute")
                    || (kind=="buff-events"&&Array.IndexOf(new[]{"apply","stack","period","expire","hurt","cast","removed"},edge.SourcePort)>=0&&edge.TargetPort=="execute")
                    || (kind=="skill-events"&&Array.IndexOf(new[]{"hit","critical","kill","completed","wall","range","projectile-end"},edge.SourcePort)>=0&&edge.TargetPort=="execute")
                    || (kind=="construct"&&Array.IndexOf(new[]{"apply","death","removed"},edge.SourcePort)>=0&&edge.TargetPort=="execute")
                    || (kind=="after-damage"&&(edge.SourcePort=="apply"||edge.SourcePort=="next")&&edge.TargetPort=="execute")
                    || ((kind == "damage" || kind == "cue" || kind=="buff" || kind=="dispel" || kind=="heal" || kind=="split" || kind=="impact"||kind=="refill-ammo"||kind=="teleport-projectile"||kind=="construct-chain") && edge.SourcePort == "next" && edge.TargetPort == "execute");
                valid &= edge.TargetPort == "target" ? _nodes[edge.Target].Kind == "cast" : _nodes[edge.Target].Kind != "target" && _nodes[edge.Target].Kind != "cast" && _nodes[edge.Target].Kind!="buff-events" && _nodes[edge.Target].Kind!="skill-events";
                if (!valid) Fail(Path + ".edges." + edge.Id, "Unsupported port connection.");
            }
            var contexts=new HashSet<string>();
            foreach(var node in Nodes)if(node.Enabled&&(node.Kind=="cast"||node.Kind=="buff-events"||node.Kind=="skill-events"))CheckProjectileContext(node,false,contexts);
            foreach(var node in _entries)CheckProjectileContext(node,false,contexts);
        }
        void CheckProjectileContext(SkillNodeDocument node,bool hit,HashSet<string> visited)
        {
            if(!node.Enabled||!visited.Add(node.Id+":"+hit))return;
            if(node.Kind=="split"&&!hit)Fail(NodePath(node),"Split requires a projectile hit context.");
            foreach(var edge in Edges)
            {
                if(edge.Source!=node.Id)continue;
                bool next=hit;
                if(node.Kind=="projectile")next=edge.SourcePort=="hit";
                if(node.Kind=="construct")next=false;
                if(node.Kind=="delivery")
                {
                    string mode=node.Parameters?.Value<string>("mode");
                    if(mode!="target"&&mode!="area"||mode=="area"&&node.Parameters?.Value<bool?>("persistent")==true)next=false;
                }
                CheckProjectileContext(_nodes[edge.Target],next,visited);
            }
        }
        public void Use(SkillNodeDocument node) => _used.Add(node.Id);
        public void IgnoreBranch(SkillNodeDocument node)
        {
            foreach (var edge in Edges)
                if (edge.Source == node.Id)
                { var child = _nodes[edge.Target]; if (_used.Add(child.Id)) IgnoreBranch(child); }
        }
        public void RequireNoOutputs(SkillNodeDocument node, params string[] ports)
        {
            foreach (var edge in Edges)
                if (edge.Source == node.Id && Array.IndexOf(ports, edge.SourcePort) >= 0)
                    Fail(Path + ".edges." + edge.Id, "Port " + edge.SourcePort + " is unavailable in this delivery mode.");
        }
        void CheckCycle(string id, HashSet<string> visiting, HashSet<string> finished)
        {
            if (finished.Contains(id)) return;
            if (!visiting.Add(id)) Fail(Path + ".nodes[" + id + "]", "Graph cycles are not allowed. Use bounded behavior parameters.");
            foreach (var edge in Edges) if (edge.Source == id) CheckCycle(edge.Target, visiting, finished);
            visiting.Remove(id); finished.Add(id);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Fail(string path, string message) => throw new ContentValidationException(path, message);
    }
}
