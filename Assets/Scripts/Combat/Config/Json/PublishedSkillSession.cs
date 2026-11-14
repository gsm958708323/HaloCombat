using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Combat.Config
{
    public sealed class PublishedSkillSession : IDisposable
    {
        public SkillSimulation Simulation { get; private set; }
        public string Revision { get; private set; }
        public IReadOnlyDictionary<string, string> Slots { get; private set; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        // Invalid input cannot destroy the last working session. Existing actors keep their
        // original definitions until a fully validated replacement world has been created.
        public void Replace(IReadOnlyDictionary<string, string> slots)
        {
            if (slots == null || slots.Count == 0 || slots.Count > 2)
                throw new ContentValidationException("$.slots", "Expected f6 and/or f7 publications.");
            var mappings = new Dictionary<string, string>();
            var skills = new Dictionary<string, SkillGraphDocument>();
            var blueprints=new Dictionary<string,SummonBlueprintDocument>();
            var buffs=new Dictionary<string,BuffDocument>();
            var revisions = new List<string>();
            SkillProjectDocument project = null;
            foreach (var slot in slots.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                if (slot.Key != "f6" && slot.Key != "f7") throw new ContentValidationException("$.slots", "Unknown slot: " + slot.Key);
                CompiledSkillPublication publication;
                try { publication = SkillPublicationJson.Read(slot.Value); }
                catch (ContentValidationException error) { throw new ContentValidationException("$.slots." + slot.Key, error.Message); }
                var document=publication.Document;var closure=SkillContentClosure.Select(document.Project,publication.Content,document.SkillId);
                if (project == null)
                    project = new SkillProjectDocument { Id = document.Project.Id, Name = document.Project.Name, Preview = document.Project.Preview,Runtime=document.Project.Runtime };
                if (project.Id != document.Project.Id)
                    throw new ContentValidationException("$.slots." + slot.Key, "Slots must be published from the same project.");
                if(SkillJsonCodec.Write(project.Runtime)!=SkillJsonCodec.Write(document.Project.Runtime))
                    throw new ContentValidationException("$.slots."+slot.Key,"Conflicting runtime Motor/Cue versions; publish both slots again.");
                foreach(var skill in closure.Skills)
                {
                    if (skills.TryGetValue(skill.Id, out var other) && SkillJsonCodec.Write(skill) != SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots." + slot.Key, "Conflicting versions of " + skill.Id + "; publish both slots again.");
                    skills[skill.Id]=skill;
                }
                foreach(var blueprint in closure.SummonBlueprints)
                {
                    if(blueprints.TryGetValue(blueprint.Id,out var other)&&SkillJsonCodec.Write(blueprint)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot.Key,"Conflicting blueprint versions: "+blueprint.Id);
                    blueprints[blueprint.Id]=blueprint;
                }
                mappings.Add(slot.Key,document.SkillId);
                foreach(var buff in closure.Buffs)
                {
                    if(buffs.TryGetValue(buff.Id,out var other)&&SkillJsonCodec.Write(buff)!=SkillJsonCodec.Write(other))
                        throw new ContentValidationException("$.slots."+slot.Key,"Conflicting Buff versions: "+buff.Id);
                    buffs[buff.Id]=buff;
                }
                revisions.Add(slot.Key + ":" + document.Revision);
            }
            project.Skills = skills.Values.ToArray();
            project.SummonBlueprints=blueprints.Values.ToArray();
            project.Buffs=buffs.Values.ToArray();
            var candidate = new SkillSimulation(project, CombatContentCompiler.Compile(project));
            var previous = Simulation;
            Simulation = candidate;
            Revision = SkillJsonCodec.Fingerprint(string.Join("\n", revisions));
            Slots = new ReadOnlyDictionary<string, string>(mappings);
            previous?.Dispose();
        }

        public void Dispose()
        { Simulation?.Dispose(); Simulation = null; Revision = null; Slots = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()); }
    }
}
