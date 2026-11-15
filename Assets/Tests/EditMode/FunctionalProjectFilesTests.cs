using System;
using System.IO;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class FunctionalProjectFilesTests
    {
        [Test]
        public void SplitFilesRoundtripAndChangingAModuleChangesTheLoadedSkill()
        {
            string directory=Path.Combine(Path.GetTempPath(),"halo-parts-"+Guid.NewGuid().ToString("N"));
            try
            {
                string index=Path.Combine(directory,"project.json");
                var project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
                string expected=SkillProjectJson.Write(project);SkillProjectFiles.Write(index,project);
                Assert.AreEqual(expected,SkillProjectJson.Write(SkillProjectFiles.Read(index)));
                var skillPath=SkillProjectFiles.ReferencedPaths(index).First(p=>p.EndsWith("basic-shot.json"));
                string original=File.ReadAllText(skillPath);File.WriteAllText(skillPath,original.Replace("普通射击","模块修改"));
                Assert.AreEqual("模块修改",SkillProjectFiles.Read(index).Skills.First(s=>s.Id=="basic-shot").Name);
                File.WriteAllText(index,File.ReadAllText(index).Replace("skills/basic-shot.json","../outside.json"));
                Assert.Throws<ContentValidationException>(()=>SkillProjectFiles.Read(index));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
