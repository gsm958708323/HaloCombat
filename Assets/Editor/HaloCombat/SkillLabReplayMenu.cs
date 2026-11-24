using System;
using System.IO;
using Combat.Config;
using Combat.Unity;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor
{
    public static class SkillLabReplayMenu
    {
        const string Load="Combat/Skill Lab/Load Input Replay...",Save="Combat/Skill Lab/Save Input Replay...";
        [MenuItem(Load)]
        public static void LoadReplay()
        {
            var lab=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();
            if(lab==null)return;
            string path=EditorUtility.OpenFilePanel("Load Skill Lab input replay","","json");if(string.IsNullOrEmpty(path))return;
            if(!lab.LoadReplayFile(path))EditorUtility.DisplayDialog("Replay could not be loaded",lab.LastLoadError,"OK");
        }
        [MenuItem(Save)]
        public static void SaveReplay()
        {
            var lab=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();if(lab==null)return;
            try
            {
                string text=lab.CaptureReplayJson();if(text==null)return;
                string path=EditorUtility.SaveFilePanel("Save Skill Lab input replay","","skill-replay","json");
                if(!string.IsNullOrEmpty(path))File.WriteAllText(path,text);
            }
            catch(Exception error) when(error is ContentValidationException||error is IOException||error is UnauthorizedAccessException)
            {EditorUtility.DisplayDialog("Replay could not be saved",error.Message,"OK");}
        }
        [MenuItem(Load,true)]public static bool CanLoad()=>EditorApplication.isPlaying&&UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>()!=null;
        [MenuItem(Save,true)]public static bool CanSave()=>CanLoad()&&UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>().Snapshot!=null;
    }
}
