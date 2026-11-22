namespace SkillLab;

internal sealed class HostPaths
{
    public string Root {get;}
    public string Data {get;}
    public string Publications {get;}
    public string ExampleProject => Path.Combine(Root, "Content", "SkillLab", "project.json");
    public string FormalProject => Path.Combine(Root, "Assets", "StreamingAssets", "Combat", "project.json");
    public string DraftProject => Path.Combine(Data, "arena", "project.json");
    public int Port {get;}
    public string Url=>"http://127.0.0.1:"+Port;
    public HostPaths(string[] args,string workingDirectory,string baseDirectory)
    {
        string Option(string name)
        {
            int index=Array.IndexOf(args,name);if(index<0)return null;
            if(index+1>=args.Length||string.IsNullOrWhiteSpace(args[index+1])||args[index+1].StartsWith("--"))throw new ArgumentException("Missing value for "+name);
            return args[index+1];
        }
        bool ContentRoot(string folder)=>File.Exists(Path.Combine(folder,"Content/SkillLab/project.json"))&&Directory.Exists(Path.Combine(folder,"Tools/SkillLab"));
        string Discover(string from)
        {
            for(var cursor=new DirectoryInfo(from);cursor!=null;cursor=cursor.Parent)if(ContentRoot(cursor.FullName))return cursor.FullName;
            return null;
        }
        Root=Path.GetFullPath(Option("--root")??Discover(baseDirectory)??Discover(workingDirectory)??throw new ArgumentException("Cannot locate SkillLab content. Pass --root <folder>."));
        if(!ContentRoot(Root))throw new ArgumentException("SkillLab root is missing Content/SkillLab/project.json or Tools/SkillLab: "+Root);
        Data=Path.GetFullPath(Option("--data-directory")??Path.Combine(Root,"Tools/SkillLab/.local"));
        Publications=Path.GetFullPath(Option("--publish-directory")??Path.Combine(Root,"Assets/StreamingAssets/Combat"));
        string port=Option("--port")??"4192";
        if(!int.TryParse(port,out int number)||number<1024||number>65535)throw new ArgumentException("Expected --port in1024..65535.");Port=number;
    }
    public bool AllowsOrigin(string origin)=>string.IsNullOrEmpty(origin)||origin==Url||origin=="http://localhost:"+Port||Port==4192&&origin=="http://127.0.0.1:5173";
}
