using Combat.Config;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkillLab;

internal static class PreviewFrames
{
    static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web){DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull};
    public static IResult Response(SkillSimulation simulation,HttpContext context,object result=null)
    {
        int afterEvent=Cursor(context,"afterEvent",0);
        bool details=context.Request.Query["details"]!="false";
        return Results.Json(new{result,state=simulation.Snapshot(afterEvent,-1,details,false),recording=simulation.Recording},Options);
    }
    static int Cursor(HttpContext context,string field,int fallback)
    {
        string value=context.Request.Query[field];if(value==null)return fallback;
        if(!int.TryParse(value,out var cursor)||cursor<fallback)throw new ContentValidationException("$."+field,"Expected a nonnegative preview cursor.");
        return cursor;
    }
}
