using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    // Candidates own an inactive view tree until gameplay and presentation both start.
    public sealed class ArenaPresentation : MonoBehaviour
    {
        ArenaPresentationBinding _binding;
        public Camera Camera=>_binding?.Camera;
        internal ArenaPresentationBinding Prepare(ArenaSession session,CombatContentSnapshot content)
        {
            if(content.Runtime?.Arena==null)throw new ContentValidationException("$.runtime.arena","Arena content is required.");
            var camera=UnityEngine.Camera.main;
            if(camera==null)throw new System.InvalidOperationException("Arena requires its main Camera.");
            return new ArenaPresentationBinding(transform,camera,session,content);
        }
        internal void Replace(ArenaPresentationBinding prepared)
        {
            prepared.Activate();var previous=_binding;_binding=prepared;previous?.Dispose();
        }
        public void Sample(float alpha,float deltaTime,float realTime)=>_binding?.Sample(alpha,deltaTime,realTime);
        public void Unbind(){_binding?.Dispose();_binding=null;}
        void OnDestroy()=>Unbind();
    }
}
