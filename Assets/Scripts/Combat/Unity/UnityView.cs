using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityActorView : MonoBehaviour, IActorView
    {
        public EntityId Id { get; private set; }
        string _blueprint;
        string _model;
        public void ConfigureModel(string model)=>_model=model;
        bool _dead;
        Transform _visual;
        Transform _core;
        Transform _ring;
        Vector3 _coreScale,_ringScale;
        float _poseTime;
        double _feedbackWallTime;
        public HitFeedbackState Feedback {get;}=new HitFeedbackState();
        readonly System.Collections.Generic.List<Material> _materials=new System.Collections.Generic.List<Material>();
        readonly System.Collections.Generic.List<Color> _baseColors=new System.Collections.Generic.List<Color>();
        public void SetAreaRadius(float radius){if(_ring!=null&&_blueprint=="aoe"){_ringScale=new Vector3(radius*2,.025f,radius*2);_ring.localScale=_ringScale;}}

        public void Bind(EntityId id, string blueprintId)
        {
            Id = id;
            _blueprint = blueprintId ?? string.Empty;
            _dead = false;
            Feedback.Reset();_poseTime=0;_feedbackWallTime=Time.realtimeSinceStartupAsDouble;
            gameObject.name = _blueprint + "_" + id.Index;
            BuildModel();
        }

        public void Sample(in PoseSample sample)
        {
            var p = sample.LogicPos;
            transform.position = new Vector3(p.X, p.Y, p.Z);
            transform.rotation = Quaternion.Euler(0f, 90f - sample.YawDeg, 0f);
            if (_dead) transform.rotation *= Quaternion.Euler(0f, 0f, 80f);
            if (_visual == null) return;
            if(!sample.InHitstop&&!_dead)_poseTime+=Time.unscaledDeltaTime;
            double now=Time.realtimeSinceStartupAsDouble;Feedback.AdvanceWall((float)(now-_feedbackWallTime));_feedbackWallTime=now;
            float bob = _blueprint == "projectile" || _blueprint == "aoe"
                ? Mathf.Sin(_poseTime * 7f + Id.Index) * 0.04f
                : Mathf.Sin(_poseTime * 4f + Id.Index) * 0.025f;
            _visual.localPosition = new Vector3(0f, bob,Feedback.Impulse*.05f);
            if (_core != null) _core.localScale = _coreScale * (1f + Mathf.Sin(_poseTime * 8f) * 0.08f);
            if (_ring != null) _ring.localScale = _ringScale * (1f + Mathf.Sin(_poseTime * 4f) * 0.05f);
            for(int i=0;i<_materials.Count;i++)if(_materials[i]!=null)_materials[i].color=Color.Lerp(_baseColors[i],Color.white,Feedback.Flash*.8f);
        }

        public void OnDead(in EvEntityDead e)
        {
            _dead = true;
            Feedback.Reset();
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        public void Release()
        {
            if (this != null) Destroy(gameObject);
        }
        public void OnHurt(in EvHurt e){if(!_dead){Feedback.Receive(e.Feedback);_feedbackWallTime=Time.realtimeSinceStartupAsDouble;}}

        void BuildModel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            string model=_model??_blueprint;
            Color main = Palette.Main(model);
            Color accent = Palette.Accent(model);
            if (model == "projectile")
            {
                _core = Part(PrimitiveType.Sphere, "ProjectileCore", new Vector3(0f, 0.28f, 0f), Vector3.one * 0.3f, accent, true);
                var trail = _core.gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.28f; trail.startWidth = 0.22f; trail.endWidth = 0.01f;
                trail.material = Material(accent, true);
            }
            else if (model == "aoe")
            {
                _ring = Part(PrimitiveType.Cylinder, "AreaField", Vector3.zero, new Vector3(2f, 0.025f, 2f), accent, true);
            }
            else if (model == "stake")
            {
                Part(PrimitiveType.Cylinder, "TargetColumn", new Vector3(0f, 0.7f, 0f), new Vector3(0.62f, 0.7f, 0.62f), main, false);
                _core = Part(PrimitiveType.Sphere, "TargetCore", new Vector3(0f, 1.35f, 0f), Vector3.one * 0.16f, accent, true);
                _ring = Part(PrimitiveType.Cylinder, "TargetRing", new Vector3(0f, 0.02f, 0f), new Vector3(1.3f, 0.015f, 1.3f), accent, true);
            }
            else if(model=="barrel"||model=="crate")
            {
                Part(model=="barrel"?PrimitiveType.Cylinder:PrimitiveType.Cube,"DestructibleBody",new Vector3(0,.35f,0),new Vector3(.5f,model=="barrel"?.35f:.7f,.5f),new Color(.45f,.22f,.08f),false);
                _ring=Part(PrimitiveType.Cylinder,"Band",new Vector3(0,.54f,0),new Vector3(.53f,.025f,.53f),new Color(.25f,.28f,.31f),false);
            }
            else
            {
                bool small = model == "summon";
                float scale = small ? 0.72f : 1f;
                Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f * scale, 0f), new Vector3(0.58f, 0.8f, 0.58f) * scale, main, false);
                Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.65f * scale, 0f), Vector3.one * 0.42f * scale, main, false);
                _core = Part(PrimitiveType.Sphere, "EnergyCore", new Vector3(0f, 1.03f * scale, -0.38f * scale), Vector3.one * 0.16f * scale, accent, true);
                Part(PrimitiveType.Cube, "Weapon", new Vector3(0.52f * scale, 0.75f * scale, 0f), new Vector3(0.12f, 0.72f, 0.18f) * scale, accent, true);
                _ring = Part(PrimitiveType.Cylinder, "GroundRing", new Vector3(0f, 0.02f, 0f), new Vector3(1.25f, 0.015f, 1.25f) * scale, accent, true);
                if (small) Part(PrimitiveType.Sphere, "SummonOrb", new Vector3(0f, 1.9f * scale, 0f), Vector3.one * 0.12f, accent, true);
            }
            _coreScale=_core!=null?_core.localScale:Vector3.one;_ringScale=_ring!=null?_ring.localScale:Vector3.one;
        }

        Transform Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(_visual, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); if (renderer != null) renderer.material = Material(color, emissive);
            return go.transform;
        }

        Material Material(Color color, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            _materials.Add(material);
            _baseColors.Add(color);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.4f);
            }
            if (color.a < 1f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.renderQueue = 3000;
            }
            return material;
        }
        void OnDestroy(){foreach(var material in _materials)if(material!=null)Destroy(material);_materials.Clear();}
    }

    public sealed class UnityViewFactory : IViewFactory
    {
        readonly System.Collections.Generic.IReadOnlyDictionary<string,RuntimeActorDefinition> _actors;
        readonly Transform _root;
        public UnityViewFactory(System.Collections.Generic.IReadOnlyDictionary<string,RuntimeActorDefinition> actors=null,Transform root=null){_actors=actors;_root=root;}
        public IActorView Create(string blueprintId)
        {
            var go = new GameObject("ActorView_" + blueprintId);
            if(_root!=null)go.transform.SetParent(_root,false);
            var view=go.AddComponent<UnityActorView>();
            if(_actors!=null&&_actors.TryGetValue(blueprintId,out var definition))view.ConfigureModel(definition.View);
            else view.ConfigureModel(blueprintId);
            return view;
        }
    }

    static class Palette
    {
        public static Color Main(string id)
        {
            if (id == "stake") return new Color(0.35f, 0.42f, 0.5f);
            if (id == "melee_guard" || id == "melee_ai" || id == "melee_ai_narrow" || id=="ranged_ai") return new Color(0.78f, 0.16f, 0.08f);
            if (id == "summon") return new Color(0.05f, 0.48f, 0.44f);
            if (id == "projectile") return new Color(0.8f, 0.12f, 0.04f);
            if (id == "aoe") return new Color(0.04f, 0.32f, 0.5f, 0.35f);
            return new Color(0.06f, 0.28f, 0.52f);
        }

        public static Color Accent(string id)
        {
            if (id == "stake") return new Color(0.9f, 0.92f, 1f);
            if (id == "melee_guard" || id == "melee_ai" || id == "melee_ai_narrow" || id=="ranged_ai") return new Color(1f, 0.26f, 0.06f);
            if (id == "summon") return new Color(0.05f, 1f, 0.78f);
            if (id == "projectile") return new Color(1f, 0.48f, 0.06f);
            if (id == "aoe") return new Color(0.08f, 0.84f, 1f, 0.4f);
            return new Color(0.08f, 0.9f, 1f);
        }
    }

}
