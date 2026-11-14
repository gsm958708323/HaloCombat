using System;
using System.IO;
using Combat.Core;

namespace Combat.Config
{
    public sealed class RuntimeProjectLoader
    {
        public string Path {get;}
        public SkillProjectDocument Project {get;}
        public CombatContentSnapshot Content {get;}
        public RuntimeProjectLoader(string path)
        {
            Path=System.IO.Path.GetFullPath(path);
            try
            {
                Project=SkillProjectJson.Read(File.ReadAllText(Path));Content=CombatContentCompiler.Compile(Project);
                if(Content.Runtime==null||Project.Runtime.Actors.Length==0)throw new ContentValidationException("$.runtime.actors","Runtime actor definitions are required.");
            }
            catch(ContentValidationException error){throw new ContentValidationException(Path,error.Message);}
        }
        public CombatWorld CreateWorld(IRandom random=null)
        {
            var runtime=Content.Runtime;var data=runtime.Data;
            var world=new CombatWorld(runtime.CreateFactory(Content.Skills),random:random??new SeededRandom(Project.Preview.Seed),
                cues:data.Cues,motor:data.Motor,skillEvents:Content.SkillEvents);
            data.Install(world);return world;
        }
    }
}
