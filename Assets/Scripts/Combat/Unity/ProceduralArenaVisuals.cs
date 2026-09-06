using UnityEngine;

namespace Combat.Unity.Game
{
    public sealed class ProceduralArenaVisuals : MonoBehaviour
    {
        public float ArenaRadius = 6f;
        public Color GroundColor = new Color(.08f, .11f, .16f);
        public Color PlayerZoneColor = new Color(.05f, .25f, .42f, .35f);
        public Color EnemyZoneColor = new Color(.42f, .08f, .07f, .35f);
        public Color BoundaryColor = new Color(1f, .5f, .08f);
        public float LightIntensity = 1.25f;
        public bool BuildOnAwake = true;

        public void Build()
        {
            if (transform.Find("ArenaGround") != null) return;
            var ground = Cube("ArenaGround", new Vector3(0, -.12f, 0), new Vector3(ArenaRadius * 2f, .2f, ArenaRadius * 2f), GroundColor);
            for (int i = -6; i <= 6; i++)
            {
                Cube("GridX" + i, new Vector3(i, .01f, 0), new Vector3(.012f, .01f, ArenaRadius * 2f), new Color(.2f, .32f, .42f, .28f));
                Cube("GridZ" + i, new Vector3(0, .012f, i), new Vector3(ArenaRadius * 2f, .01f, .012f), new Color(.2f, .32f, .42f, .28f));
            }
            Cube("PlayerZone", new Vector3(-3f, .015f, 0), new Vector3(5.8f, .012f, 11.2f), PlayerZoneColor);
            Cube("EnemyZone", new Vector3(3f, .016f, 0), new Vector3(5.8f, .012f, 11.2f), EnemyZoneColor);
            var corners = new[] { new Vector3(-ArenaRadius, .45f, -ArenaRadius), new Vector3(-ArenaRadius, .45f, ArenaRadius), new Vector3(ArenaRadius, .45f, -ArenaRadius), new Vector3(ArenaRadius, .45f, ArenaRadius) };
            for (int i = 0; i < corners.Length; i++)
                Cube("Corner" + i, corners[i], new Vector3(.25f, .9f, .25f), BoundaryColor);
            Cube("WallN", new Vector3(0, .28f, ArenaRadius), new Vector3(ArenaRadius * 2f, .55f, .12f), BoundaryColor);
            Cube("WallS", new Vector3(0, .28f, -ArenaRadius), new Vector3(ArenaRadius * 2f, .55f, .12f), BoundaryColor);
            Cube("WallE", new Vector3(ArenaRadius, .28f, 0), new Vector3(.12f, .55f, ArenaRadius * 2f), BoundaryColor);
            Cube("WallW", new Vector3(-ArenaRadius, .28f, 0), new Vector3(.12f, .55f, ArenaRadius * 2f), BoundaryColor);
            var key = new GameObject("ArenaKeyLight"); key.transform.SetParent(transform, false);
            var light = key.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = LightIntensity; key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(.12f, .16f, .22f);
        }

        void Awake() { if (BuildOnAwake) Build(); }

        GameObject Cube(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(transform, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>(); if (r != null) { r.material.color = color; r.material.EnableKeyword("_EMISSION"); r.material.SetColor("_EmissionColor", color * .35f); }
            Object.Destroy(go.GetComponent<Collider>()); return go;
        }
    }
}
