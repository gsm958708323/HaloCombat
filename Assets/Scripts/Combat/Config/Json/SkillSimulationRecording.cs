using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public sealed partial class SkillSimulation
    {
        readonly List<SkillReplayInput> _recordedInputs=new List<SkillReplayInput>();
        string _replayProjectJson,_recordingError;
        public SkillReplayStatus Recording=>new SkillReplayStatus{Inputs=_recordedInputs.Count,Steps=_steps,Error=_recordingError??(_steps>SkillReplayJson.MaximumSteps?"记录超过600秒，请重置后重新记录。":null)};
        void InitializeRecording(SkillProjectDocument project)
        {
            var copy=SkillProjectJson.Read(SkillProjectJson.Write(project));SkillJsonTransfer.NormalizeLayout(copy);_replayProjectJson=SkillProjectJson.Write(copy);
        }
        public object Execute(SkillSimulationCommand command)
        {
            EnsureAlive();SkillSimulationCommands.Validate(command);object result=null;int at=_steps;
            switch(command.Kind)
            {
                case "step":Advance(command.Steps??1);break;
                case "snapshot":break;
                case "cast":result=Cast(command.SkillId,command.X??0,command.Z??0);break;
                case "input":Input(command.Input,command.X??0,command.Z??0);break;
                case "release":if(command.X.HasValue)Aim(command.X.Value,command.Z.Value);Release();break;
                case "cancel":CancelInput();break;
                case "aim":Aim(command.X??0,command.Z??0);break;
                case "move":Move(command.X??0,command.Z??0);break;
                case "place":Place(command.ActorId,command.X??0,command.Z??0);break;
                case "remove":Remove(command.EntityId);break;
                case "remove-buff":result=RemoveBuff(command.ActorId,command.InstanceId.Value);break;
                case "interrupt":Interrupt();break;
                case "death":Interrupt(true);break;
                case "hitstop":RequestHitstop(command.Frames??10);break;
            }
            if(command.Kind!="step"&&command.Kind!="snapshot"&&_recordingError==null)
            {
                if(_recordedInputs.Count>=SkillReplayJson.MaximumInputs)_recordingError="记录超过10000次输入，请重置后重新记录。";
                else _recordedInputs.Add(new SkillReplayInput{AtStep=at,Command=SkillSimulationCommands.Read(SkillJsonCodec.Write(command))});
            }
            return result;
        }
        public SkillReplayDocument CaptureReplay(string rootSkillId)
        {
            EnsureAlive();if(Recording.Error!=null)throw new ContentValidationException("$.inputs",Recording.Error);
            var document=new SkillReplayDocument{Project=SkillProjectJson.Read(_replayProjectJson),RootSkillId=rootSkillId,EndStep=_steps,
                Inputs=_recordedInputs.ToArray(),ExpectedState=JObject.Parse(SkillJsonCodec.Write(Snapshot()))};
            // The returned value owns its DTOs; editing an exported record cannot mutate the live journal.
            return SkillReplayJson.Read(SkillReplayJson.Write(document));
        }
    }
}
