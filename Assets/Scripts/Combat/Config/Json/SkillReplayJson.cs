using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public static class SkillReplayJson
    {
        public const int MaximumSteps=30000,MaximumInputs=10000;
        public static string Write(SkillReplayDocument document)=>SkillJsonCodec.Write(document);
        public static SkillReplayDocument Read(string text)
        {
            if(string.IsNullOrWhiteSpace(text))throw new ContentValidationException("$","Replay file text is required.");
            var document=SkillJsonCodec.Read<SkillReplayDocument>(text);
            var raw=JObject.Parse(text);
            foreach(string field in new[]{"formatVersion","endStep"})if(raw[field]?.Type!=JTokenType.Integer)throw new ContentValidationException("$."+field,"Expected an integer.");
            if(document.FormatVersion!=1)throw new ContentValidationException("$.formatVersion","Only replay version1 is supported.");
            if(document.StepSeconds!=SkillSimulation.Step)throw new ContentValidationException("$.stepSeconds","Replay step must be0.02 seconds.");
            if(document.EndStep<0||document.EndStep>MaximumSteps)throw new ContentValidationException("$.endStep","Expected0..30000 steps.");
            if(document.Inputs==null||document.Inputs.Length>MaximumInputs)throw new ContentValidationException("$.inputs","Expected at most10000 inputs.");
            if(raw["project"]?["preview"]?["seed"]!=null&&raw["project"]["preview"]["seed"].Type!=JTokenType.Integer)
                throw new ContentValidationException("$.project.preview.seed","Expected a32-bit integer seed.");
            SkillProjectStructure.ValidateRuntimeStructure(document.Project);
            if(!document.Project.Skills.Any(s=>s.Id==document.RootSkillId))throw new ContentValidationException("$.rootSkillId","Replay root skill is missing.");
            int previous=0;
            for(int i=0;i<document.Inputs.Length;i++)
            {
                var input=document.Inputs[i];string path="$.inputs["+i+"]";
                if(input==null||input.AtStep<previous||input.AtStep>document.EndStep)throw new ContentValidationException(path+".atStep","Input steps must be ordered and within endStep.");
                if(raw["inputs"][i]["atStep"]?.Type!=JTokenType.Integer)throw new ContentValidationException(path+".atStep","Expected an integer.");
                try{SkillSimulationCommands.ValidateToken(raw["inputs"][i]["command"] as JObject);SkillSimulationCommands.Validate(input.Command);}
                catch(ContentValidationException error){throw new ContentValidationException(path+".command"+error.Path.Substring(1),error.Message);}
                if(input.Command.Kind=="step"||input.Command.Kind=="snapshot")throw new ContentValidationException(path+".command.kind","Replay stores inputs, not stepping or snapshots.");
                previous=input.AtStep;
            }
            if(document.ExpectedState==null||document.ExpectedState["step"]?.Type!=JTokenType.Integer||document.ExpectedState["step"].ToString()!=document.EndStep.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new ContentValidationException("$.expectedState.step","Expected state must describe endStep.");
            foreach(var value in document.ExpectedState.Descendants().OfType<JValue>())
                if(value.Type==JTokenType.Float&&(double.IsNaN(value.Value<double>())||double.IsInfinity(value.Value<double>())))
                    throw new ContentValidationException("$.expectedState."+value.Path,"Expected finite snapshot numbers.");
            return document;
        }
    }
    public sealed class SkillReplayRun:IDisposable
    {
        bool _transferred;
        public SkillSimulation Simulation {get;}
        public SkillReplayComparison Comparison {get;}
        public SkillReplayRun(SkillReplayDocument document)
        {
            document=SkillReplayJson.Read(SkillReplayJson.Write(document));
            Simulation=new SkillSimulation(document.Project,CombatContentCompiler.Compile(document.Project));
            try
            {
                int step=0;
                foreach(var input in document.Inputs)
                {
                    Advance(input.AtStep-step);step=input.AtStep;Simulation.Execute(input.Command);
                }
                Advance(document.EndStep-step);
                Comparison=SkillReplayComparer.Compare(document.ExpectedState,SkillReplayState.Capture(Simulation.Snapshot()));
            }
            catch{Simulation.Dispose();throw;}
        }
        void Advance(int steps){while(steps>0){int count=Math.Min(500,steps);Simulation.Execute(new SkillSimulationCommand{Kind="step",Steps=count});steps-=count;}}
        public SkillSimulation TakeSimulation()
        {if(_transferred)throw new InvalidOperationException("Replay simulation ownership was already transferred.");_transferred=true;return Simulation;}
        public void Dispose(){if(!_transferred)Simulation.Dispose();}
    }
}
