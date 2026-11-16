using System.Collections.Generic;
using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class SkillLabDamageNumbers
    {
        struct Number
        {
            public float X,Z,Amount,Started;
            public bool Critical;
        }
        readonly List<Number> _numbers=new List<Number>();
        int _lastSequence;
        GUIStyle _normal,_critical;
        public int Count=>_numbers.Count;
        public void Clear(){_numbers.Clear();_lastSequence=0;}
        public void Update(SkillSimulationSnapshot snapshot)
        {
            float now=Time.unscaledTime;
            _numbers.RemoveAll(n=>now-n.Started>=1.1f);
            foreach(var e in snapshot.Events)
            {
                if(e.Sequence<=_lastSequence)continue;_lastSequence=e.Sequence;
                if(e.Damage==null||(e.Kind!="damage"&&e.Kind!="critical")||!e.TargetX.HasValue||!e.TargetZ.HasValue)continue;
                float amount=e.Damage.Absorbed+e.Damage.HpLost;if(amount<=0)continue;
                if(_numbers.Count==96)_numbers.RemoveAt(0);
                _numbers.Add(new Number{X=e.TargetX.Value,Z=e.TargetZ.Value,Amount=amount,Critical=e.Kind=="critical",Started=now});
            }
        }
        public void Draw(Camera camera)
        {
            if(_normal==null)
            {
                _normal=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=18};_normal.normal.textColor=Color.white;
                _critical=new GUIStyle(_normal){fontSize=23};_critical.normal.textColor=new Color(1,.76f,.25f);
            }
            foreach(var n in _numbers)
            {
                float age=Time.unscaledTime-n.Started;var point=camera.WorldToScreenPoint(new Vector3(n.X,1.4f,n.Z));
                GUI.Label(new Rect(point.x-60,Screen.height-point.y-35-age*35,120,35),n.Amount.ToString("0.#")+(n.Critical?"!":""),n.Critical?_critical:_normal);
            }
        }
    }
}
