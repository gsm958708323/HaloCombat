using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    // Author diagnostics explain execution but never change format-1 gameplay outcomes
    // or the original gameplay event identities used by saved recordings.
    internal static class SkillReplayState
    {
        internal static JObject Capture(SkillSimulationSnapshot snapshot)
        {
            var result=JObject.Parse(SkillJsonCodec.Write(snapshot));
            result.Property("comboDecisions")?.Remove();
            foreach(var actor in (JArray)result["actors"])
            {((JObject)actor).Property("comboDecision")?.Remove();((JObject)actor).Property("comboInputSequence")?.Remove();}
            foreach (JObject entry in (JArray)result["events"])
            {
                entry.Remove("comboDecision");
                entry.Remove("periodicBuff");
            }
            return result;
        }
    }
}
