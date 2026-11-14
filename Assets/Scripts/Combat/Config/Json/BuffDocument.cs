using System;
using Combat.Core;

namespace Combat.Config
{
    public sealed class BuffDocument
    {
        public string Id {get;set;}
        public int RuntimeId {get;set;}
        public string Name {get;set;}
        public bool Permanent {get;set;}
        public float Duration {get;set;}=2;
        public float Interval {get;set;}
        public string Stack {get;set;}="refresh";
        public int MaxStacks {get;set;}=1;
        public int MutexGroup {get;set;}
        public int[] Tags {get;set;}=Array.Empty<int>();
        public BuffModifierDocument[] Modifiers {get;set;}=Array.Empty<BuffModifierDocument>();
        public SkillNodeDocument[] Nodes {get;set;}=new[]{new SkillNodeDocument {Id="events",Kind="buff-events",Position=new NodePosition {X=0,Y=80}}};
        public SkillEdgeDocument[] Edges {get;set;}=Array.Empty<SkillEdgeDocument>();
    }
    public sealed class BuffModifierDocument
    {
        public string Attribute {get;set;}="moveSpeed";
        public string Operation {get;set;}="mul";
        public float Value {get;set;}=.5f;
        public int Priority {get;set;}
    }
}
