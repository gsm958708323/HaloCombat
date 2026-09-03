using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityCuePlayer : ICuePlayer
    {
        readonly CueLibraryAsset _assets;
        public UnityCuePlayer(CueLibraryAsset assets) => _assets = assets;

        public void Play(EvCue e)
        {
            GameObject prefab = _assets != null ? _assets.FindPrefab(e.CueId) : null;
            var gameObject = prefab != null
                ? Object.Instantiate(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (prefab == null)
            {
                var collider = gameObject.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                gameObject.transform.localScale = Vector3.one * 0.2f;
            }

            gameObject.name = "Cue_" + e.CueId;
            Object.Destroy(gameObject, 0.45f);
        }
    }

    public sealed class UnityFloater : IFloaterPlayer
    {
        public void Play(EvDamage e)
        {
            Debug.Log("DMG " + e.Amount.ToString("F0") + (e.IsCrit ? " CRIT" : "") + " src=" + e.Source);
        }

        public void PlayImmune(EvImmune e) => Debug.Log("IMMUNE " + e.Target);
    }

    public sealed class UnityHitstopOverlay : IHitstopOverlay
    {
        float _flash;
        Texture2D _texture;

        public void ShowFlash() => _flash = 0.08f;
        public void SetActive(bool on) { }

        public void TickWall(float dt)
        {
            if (_flash > 0f) _flash -= dt;
        }

        public void OnGUI()
        {
            if (_flash <= 0f) return;
            if (_texture == null)
            {
                _texture = new Texture2D(1, 1);
                _texture.SetPixel(0, 0, Color.white);
                _texture.Apply();
            }
            var color = new Color(1f, 1f, 1f, _flash / 0.08f * 0.25f);
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texture);
            GUI.color = Color.white;
        }
    }
}
