using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public static class SkillReplayComparer
    {
        public static SkillReplayComparison Compare(JToken expected,JToken actual)
        {
            var result=new SkillReplayComparison();var differences=new List<SkillReplayDifference>();
            Compare(expected,actual,"$",result,differences);result.Differences=differences.ToArray();return result;
        }
        static void Compare(JToken expected,JToken actual,string path,SkillReplayComparison result,List<SkillReplayDifference> differences)
        {
            if(differences.Count>=32)return;
            if(expected is JObject a&&actual is JObject b)
            {
                foreach(var key in a.Properties().Select(p=>p.Name).Union(b.Properties().Select(p=>p.Name)))
                    Compare(a[key],b[key],path+"."+key,result,differences);
            }
            else if(expected is JArray x&&actual is JArray y)
            {
                if(x.Count!=y.Count)Difference(path+".length",new JValue(x.Count),new JValue(y.Count),differences);
                for(int i=0;i<Math.Min(x.Count,y.Count);i++)Compare(x[i],y[i],path+"["+i+"]",result,differences);
            }
            else
            {
                result.Values++;
                if(Numeric(expected)&&Numeric(actual))
                {
                    double difference=Math.Abs(expected.Value<double>()-actual.Value<double>());
                    bool finite=!double.IsNaN(difference)&&!double.IsInfinity(difference);
                    result.MaximumNumericDifference=Math.Max(result.MaximumNumericDifference,finite?difference:double.MaxValue);
                    bool exact=expected.Type==JTokenType.Integer||actual.Type==JTokenType.Integer;
                    if(!finite||difference>(exact?0:result.Tolerance))Difference(path,expected,actual,differences);
                }
                else if(!JToken.DeepEquals(expected,actual))Difference(path,expected,actual,differences);
            }
        }
        static bool Numeric(JToken value)=>value!=null&&(value.Type==JTokenType.Integer||value.Type==JTokenType.Float);
        static void Difference(string path,JToken expected,JToken actual,List<SkillReplayDifference> differences)
        {if(differences.Count<32)differences.Add(new SkillReplayDifference{Path=path,Expected=expected?.ToString()??"missing",Actual=actual?.ToString()??"missing"});}
    }
}
