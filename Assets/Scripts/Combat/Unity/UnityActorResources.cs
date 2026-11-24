using System;
using System.Collections.Generic;
using Combat.Config;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Combat.Unity
{
    internal static class UnityActorResources
    {
        internal static void Validate(RuntimeContentSnapshot content)
            =>Validate(content.Views);
        internal static void Validate(IReadOnlyDictionary<string,RuntimeViewDefinition> views)
        {
            if(views==null)return;
            foreach(var view in views.Values)
            {
                string path="$.runtime.views["+view.Id+"]";
                var prefab=Resources.Load<GameObject>(view.Resource);
                if(prefab==null)throw new ContentValidationException(path+".resource","Missing Unity prefab resource: "+view.Resource);
                foreach(var anchor in view.Anchors)if(anchor.Path.Length>0&&prefab.transform.Find(anchor.Path)==null)throw new ContentValidationException(path+".anchors["+anchor.Key+"]","Missing anchor transform: "+anchor.Path);
                if(view.Kind=="body")
                {
                    if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||prefab.GetComponentsInChildren<Collider>(true).Length!=0||prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0||prefab.GetComponentsInChildren<Animator>(true).Length!=0||prefab.GetComponentsInChildren<Renderer>(true).Length==0)
                        throw new ContentValidationException(path+".resource","Body presentation requires renderers and no gameplay scripts, physics or character Animator.");
                    continue;
                }
                if(prefab.GetComponentsInChildren<Animator>(true).Length!=1||prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0||
                    prefab.GetComponentsInChildren<Collider>(true).Length!=0||prefab.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                    throw new ContentValidationException(path+".resource","Actor presentation requires one Animator and no gameplay scripts, colliders or rigidbodies.");
                GameObject candidate=null;
                try
                {
                    candidate=Object.Instantiate(prefab);candidate.hideFlags=HideFlags.HideAndDontSave;
                    var animator=candidate.GetComponentInChildren<Animator>(true);
                    if(animator.runtimeAnimatorController==null||animator.avatar==null||!animator.avatar.isValid)
                        throw new ContentValidationException(path+".resource","Animator requires a controller and valid avatar.");
                    animator.applyRootMotion=false;animator.fireEvents=false;animator.speed=0;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    candidate.SetActive(true);animator.Rebind();animator.Update(0);
                    foreach(var state in view.States)
                    {
                        int hash=Animator.StringToHash(state.Value);
                        if(!animator.HasState(0,hash))throw new ContentValidationException(path+".states["+state.Key+"]","Missing Animator state: "+state.Value);
                        animator.Play(hash,0,0);animator.Update(0);
                        var clips=animator.GetCurrentAnimatorClipInfo(0);
                        if(clips.Length!=1||clips[0].clip==null||clips[0].clip.length<=0)throw new ContentValidationException(path+".states["+state.Key+"]","Animator state requires one playable AnimationClip.");
                    }
                }
                finally
                {
                    if(candidate!=null){candidate.SetActive(false);if(Application.isPlaying)Object.Destroy(candidate);else Object.DestroyImmediate(candidate);}
                }
            }
        }
    }
}
