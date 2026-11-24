using System;
using System.Collections.Generic;
using System.IO;
using Combat.Config;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public static class UnityRuntimeContent
    {
        public const string DefaultFile="Combat/project.json";
        public static void RequireActors(RuntimeProjectLoader loaded,params string[] identities)
        {foreach(string id in identities)if(!loaded.Content.Runtime.Actors.ContainsKey(id))throw new ContentValidationException(loaded.Path+".runtime.actors","Required scene blueprint is missing: "+id);}
        public static RuntimeProjectLoader Load(string relativeFile=DefaultFile)
        {
            if(string.IsNullOrWhiteSpace(relativeFile)||System.IO.Path.IsPathRooted(relativeFile))throw new ContentValidationException("contentFile","Expected StreamingAssets relative JSON path.");
            string root=System.IO.Path.GetFullPath(Application.streamingAssetsPath)+System.IO.Path.DirectorySeparatorChar;
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relativeFile));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new ContentValidationException("contentFile","Content path leaves StreamingAssets.");
            var loaded=new RuntimeProjectLoader(path);
            UnityActorResources.Validate(loaded.Content.Runtime);
            foreach(var cue in loaded.Project.Runtime.Cues)
            {
                if(cue.Kind=="prefab"&&Resources.Load<GameObject>(cue.Resource)==null)throw new ContentValidationException(path+".runtime.cues["+cue.Id+"].resource","Missing Unity prefab resource: "+cue.Resource);
                if(!string.IsNullOrEmpty(cue.Sound)&&Resources.Load<AudioClip>(cue.Sound)==null)throw new ContentValidationException(path+".runtime.cues["+cue.Id+"].sound","Missing Unity audio resource: "+cue.Sound);
            }
            return loaded;
        }
    }
}
