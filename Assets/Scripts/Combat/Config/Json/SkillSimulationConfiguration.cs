using Combat.Core;

namespace Combat.Config
{
    public sealed partial class SkillSimulation
    {
        // Candidates compile completely before any live reference changes. Existing
        // actions, projectiles, areas and Buffs keep their captured definitions.
        public void ApplyConfiguration(SkillProjectDocument project)
        {
            EnsureAlive();var content=CombatContentCompiler.Compile(project);
            foreach(var actor in _world.RegistryActive())
            {
                if(actor.TryGetComp<SkillCastComp>(out var cast))cast.ReplaceCatalog(content.Skills);
                if(actor.TryGetComp<SkillDirectorComp>(out var director))director.ReplaceLibrary(content.Timelines);
                if(actor.TryGetComp<ComboComp>(out var combo))combo.ReplaceTable(content.Runtime?.Data.Combo??new ComboTableDefinition());
                if(actor.TryGetComp<InputBufferComp>(out var inputs))inputs.SetBufferWindow(content.Runtime?.Arena!=null?content.Runtime.Actors[content.Runtime.Arena.Player].InputBufferSeconds:project.Preview.InputBufferSeconds);
            }
            _factory.ReplaceContent(content);_trace.Register(content.Sources);
            _world.ReplaceMotor(content.Runtime?.Data.Motor??MotorConfig.SeasonOneDefaults());
            _world.ReplaceCatalogs(content.Projectiles,content.Aoes,content.Summons);
            _world.ReplaceConstructs(content.Constructs);_world.ReplaceSkillEvents(content.SkillEvents);
            _world.ReplaceCues(content.Runtime?.Data.Cues??content.Cues);_content=content;
            Record("configuration","editor","技能配置已应用；场景和存量实例保留");
        }
    }
}
