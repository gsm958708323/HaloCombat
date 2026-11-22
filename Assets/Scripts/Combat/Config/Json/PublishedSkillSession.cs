using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Combat.Config
{
    public sealed class PublishedSkillSession : IDisposable
    {
        public SkillSimulation Simulation { get; private set; }
        public Action<IReadOnlyDictionary<string,RuntimeViewDefinition>> ValidateViews {get;set;}
        public string Revision { get; private set; }
        public bool HasDifferentSlotLayouts { get; private set; }
        public IReadOnlyDictionary<string, string> Slots { get; private set; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        public IReadOnlyDictionary<string,string> SlotRevisions {get;private set;}=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>());

        // Invalid input cannot destroy the last working session. Existing actors keep their
        // original definitions until a fully validated replacement world has been created.
        public void Replace(IReadOnlyDictionary<string, string> slots)
        {
            if (slots == null || slots.Count == 0)
                throw new ContentValidationException("$.slots", "Expected at least one publication.");
            foreach (var name in slots.Keys)
                if (!PublishedSkillSlots.Ordered.Contains(name))
                    throw new ContentValidationException("$.slots", "Unknown slot: " + name);
            var mappings = new Dictionary<string, string>();
            var slotRevisions=new Dictionary<string,string>();
            var skills = new Dictionary<string, SkillGraphDocument>();
            var blueprints=new Dictionary<string,SummonBlueprintDocument>();
            var buffs=new Dictionary<string,BuffDocument>();
            var timelines=new Dictionary<string,SharedTimelineDocument>();
            var projectiles=new Dictionary<string,SharedProjectileDocument>();
            var areas=new Dictionary<string,SharedAreaDocument>();
            var revisions = new List<string>();
            SkillProjectDocument project = null;
            bool differentLayouts = false;
            foreach (var slot in PublishedSkillSlots.Ordered)
            {
                if (!slots.TryGetValue(slot, out var text)) continue;
                CompiledSkillPublication publication;
                try { publication = SkillPublicationJson.Read(text); }
                catch (ContentValidationException error) { throw new ContentValidationException("$.slots." + slot, error.Message); }
                var document=publication.Document;var closure=SkillContentClosure.Select(document.Project,publication.Content,document.SkillId);
                if (project == null)
                    project = new SkillProjectDocument { Id = document.Project.Id, Name = document.Project.Name, Preview = document.Project.Preview,Runtime=document.Project.Runtime };
                    else if (SkillJsonCodec.Write(project.Preview) != SkillJsonCodec.Write(document.Project.Preview))
                    differentLayouts = true;
                if (project.Id != document.Project.Id)
                    throw new ContentValidationException("$.slots." + slot, "Slots must be published from the same project.");
                if(SkillJsonCodec.Write(project.Runtime)!=SkillJsonCodec.Write(document.Project.Runtime))
                    throw new ContentValidationException("$.slots."+slot,"Conflicting runtime Motor/Cue/View versions; republish the occupied slots.");
                foreach(var skill in closure.Skills)
                {
                    if (skills.TryGetValue(skill.Id, out var other) && SkillJsonCodec.Write(skill) != SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots." + slot, "Conflicting versions of " + skill.Id + "; republish the occupied slots.");
                    skills[skill.Id]=skill;
                }
                foreach(var blueprint in closure.SummonBlueprints)
                {
                    if(blueprints.TryGetValue(blueprint.Id,out var other)&&SkillJsonCodec.Write(blueprint)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot,"Conflicting blueprint versions: "+blueprint.Id);
                    blueprints[blueprint.Id]=blueprint;
                }
                mappings.Add(slot,document.SkillId);
                slotRevisions.Add(slot,document.Revision);
                foreach(var buff in closure.Buffs)
                {
                    if(buffs.TryGetValue(buff.Id,out var other)&&SkillJsonCodec.Write(buff)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot,"Conflicting Buff versions: "+buff.Id);
                    buffs[buff.Id]=buff;
                }
                foreach(var timeline in closure.Timelines)
                {
                    if(timelines.TryGetValue(timeline.Id,out var other)&&SkillJsonCodec.Write(timeline)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot,"Conflicting timeline versions: "+timeline.Id);
                    timelines[timeline.Id]=timeline;
                }
                foreach(var area in closure.Areas)
                {
                    if(areas.TryGetValue(area.Id,out var other)&&SkillJsonCodec.Write(area)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot,"Conflicting area versions: "+area.Id);
                    areas[area.Id]=area;
                }
                revisions.Add(slot + ":" + document.Revision);
                foreach(var projectile in closure.Projectiles)
                {
                    if(projectiles.TryGetValue(projectile.Id,out var other)&&SkillJsonCodec.Write(projectile)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot,"Conflicting projectile versions: "+projectile.Id);
                    projectiles[projectile.Id]=projectile;
                }
            }
            project.Skills = skills.Values.ToArray();
            project.SummonBlueprints=blueprints.Values.ToArray();
            project.Buffs=buffs.Values.ToArray();
            project.Timelines=timelines.Values.ToArray();
            project.Projectiles=projectiles.Values.ToArray();
            project.Areas=areas.Values.ToArray();
            var compiled=CombatContentCompiler.Compile(project);ValidateViews?.Invoke(compiled.Runtime?.Views);
            var candidate = new SkillSimulation(project,compiled);
            var previous = Simulation;
            Simulation = candidate;
            HasDifferentSlotLayouts = differentLayouts;
            Revision = SkillJsonCodec.Fingerprint(string.Join("\n", revisions));
            Slots = new ReadOnlyDictionary<string, string>(mappings);
            SlotRevisions=new ReadOnlyDictionary<string,string>(slotRevisions);
            previous?.Dispose();
        }

        public void Dispose()
        { Simulation?.Dispose(); Simulation = null; Revision = null; Slots = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()); }

        public SkillReplayComparison ReplaceReplay(string text)
        {
            var document=SkillReplayJson.Read(text);
            using(var run=new SkillReplayRun(document))
            {
                ValidateViews?.Invoke(run.Simulation.Views);
                if(!run.Comparison.Matches)throw new ContentValidationException("$.expectedState","Replay differs at "+run.Comparison.Differences[0].Path);
                var previous=Simulation;Simulation=run.TakeSimulation();Revision=SkillJsonCodec.Fingerprint(text);
                Slots=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>{{PublishedSkillSlots.Ordered[0],document.RootSkillId}});
                SlotRevisions=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>{{PublishedSkillSlots.Ordered[0],Revision}});
                previous?.Dispose();return run.Comparison;
            }
        }
    }
}
