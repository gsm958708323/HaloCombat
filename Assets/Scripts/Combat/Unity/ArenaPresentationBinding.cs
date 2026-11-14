using System;
using System.Collections.Generic;
using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Combat.Unity
{
    internal sealed class ArenaPresentationBinding : IDisposable
    {
        readonly ArenaSession _session;
        readonly List<Material> _materials=new List<Material>();
        readonly Transform _root;
        readonly ViewRegistry _views;
        readonly PresentationRouter _router;
        bool _disposed;
        public Camera Camera {get;}
        public ArenaPresentationBinding(Transform parent,Camera camera,ArenaSession session,CombatContentSnapshot content)
        {
            _session=session;Camera=camera;_root=new GameObject("ArenaRuntime").transform;
            _root.gameObject.SetActive(false);_root.SetParent(parent,false);
            try
            {
                var views=new GameObject("ActorViews").transform;views.SetParent(_root,false);
                var vfx=new GameObject("Effects").transform;vfx.SetParent(_root,false);
                _views=new ViewRegistry(new UnityViewFactory(content.Runtime.Actors,views,content.Runtime.Views));
                _router=new PresentationRouter(session.World,_views,new UnityCuePlayer(content.Runtime.Data.Cues,vfx,session.World));
                _router.Bind(session.World.Events);
                var rules=session.Definition;
                Block("Ground",new Vector3(0,session.World.Motor.GroundY-.1f,0),new Vector3(rules.HalfWidth*2,.15f,rules.HalfDepth*2),new Color(.08f,.13f,.17f));
                foreach(var box in rules.Obstacles)Block("Obstacle_"+box.Id,new Vector3(box.X,session.World.Motor.GroundY+.3f,box.Z),new Vector3(box.Width,.6f,box.Depth),new Color(.2f,.3f,.33f));
            }
            catch{Dispose();throw;}
        }
        public void Activate()
        {
            _root.gameObject.SetActive(true);Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.025f,.04f,.065f);
            RenderSettings.ambientLight=new Color(.5f,.55f,.65f);RenderSettings.fog=false;
        }
        void Block(string name,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(_root,false);go.transform.position=position;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(collider!=null)Object.Destroy(collider);
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Unlit/Color");
            if(shader==null)throw new InvalidOperationException("Arena surface shader is unavailable.");
            var material=new Material(shader){color=color};_materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
        }
        public void Sample(float alpha)
        {
            if(_disposed)return;_router.SampleLate(alpha,_session.Paused);
            if(_session.World.TryGetActor(_session.PlayerId,out var player))
            {
                var p=player.GetComp<TransformComp>().Position;var definition=_session.Definition;var target=new Vector3(p.X,p.Y+definition.CameraFocusHeight,p.Z);
                Camera.transform.position=target+new Vector3(0,definition.CameraHeight,-definition.CameraDistance);Camera.transform.LookAt(target);Camera.fieldOfView=definition.CameraFov;
            }
            foreach(var body in _root.GetComponentsInChildren<UnityActorView>())
                if(_session.World.TryGetActor(body.Id,out var actor)&&actor.TryGetComp<AoeComp>(out var area))body.SetAreaRadius(area.Radius);
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;_router?.Unbind(_session.World.Events);_views?.ReleaseAll();
            _root.gameObject.SetActive(false);Object.Destroy(_root.gameObject);
            foreach(var material in _materials)if(material!=null)Object.Destroy(material);_materials.Clear();
        }
    }
}
