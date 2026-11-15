namespace SkillLab;

internal static class HostPathChecks
{
    public static void Run(string root)
    {
        var defaultPaths=new HostPaths(Array.Empty<string>(),root,Path.Combine(root,"Tools/SkillLab/bin/Debug/net10.0"));
        if(defaultPaths.Root!=Path.GetFullPath(root)||defaultPaths.Port!=4192)throw new Exception("Source host root discovery changed.");
        if(!defaultPaths.AllowsOrigin("http://127.0.0.1:4192")||!defaultPaths.AllowsOrigin("http://127.0.0.1:5173")||defaultPaths.AllowsOrigin("https://example.com"))throw new Exception("Host origin restrictions changed.");
        string temp=Path.Combine(Path.GetTempPath(),"SkillLab-Packaged-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            Directory.CreateDirectory(Path.Combine(temp,"Content/SkillLab"));File.WriteAllText(Path.Combine(temp,"Content/SkillLab/project.json"),"{}");
            var binary=Path.Combine(temp,"Tools/SkillLab/HostRuntime");Directory.CreateDirectory(binary);
            var packaged=new HostPaths(new[]{"--port","4193","--data-directory",Path.Combine(temp,"Data"),"--publish-directory",Path.Combine(temp,"Publications")},root,binary);
            if(packaged.Root!=temp||packaged.Port!=4193||!packaged.AllowsOrigin("http://localhost:4193")||packaged.AllowsOrigin("http://127.0.0.1:4192"))throw new Exception("Packaged root must take precedence over a different working checkout.");
            foreach(string[] args in new[]{new[]{"--port","80"},new[]{"--port","invalid"},new[]{"--root"},new[]{"--root",Path.Combine(temp,"missing")}}){
                bool rejected=false;try{new HostPaths(args,root,binary);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid startup arguments were accepted.");
            }
        }finally{Directory.Delete(temp,true);}
        Console.WriteLine("PASS: repository/packaged root discovery, explicit isolated data/publication paths, loopback ports and exact origin policy.");
    }
}
