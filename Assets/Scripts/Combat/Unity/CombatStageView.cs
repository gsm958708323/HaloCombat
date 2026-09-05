using UnityEngine;

namespace Combat.Unity
{
    public static class CombatStageView
    {
        static GameObject _root;
        public static void Ensure(Camera camera)
        {
            if (_root == null)
            {
                _root = new GameObject("CombatLessonStage");
                BuildArena(_root.transform);
            }
            if (camera == null) return;
            camera.transform.position = new Vector3(0f, 9.5f, -14f);
            camera.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.015f, 0.045f, 1f);
            RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.02f, 0.07f);
            RenderSettings.fogDensity = 0.012f;
        }

        static void BuildArena(Transform root)
        {
            var floor = Primitive(PrimitiveType.Cylinder, "ArenaFloor", root, Vector3.zero, new Vector3(5.8f, 0.08f, 4.7f), new Color(0.025f, 0.05f, 0.12f), false);
            var inner = Primitive(PrimitiveType.Cylinder, "ArenaInner", root, new Vector3(0f, 0.09f, 0f), new Vector3(5.15f, 0.018f, 4.05f), new Color(0.04f, 0.09f, 0.18f), false);
            Ring(root, "ArenaBorder", 5.7f, 4.6f, 0.035f, new Color(0.03f, 0.6f, 0.95f, 0.8f));
            Ring(root, "ArenaInnerRing", 4.4f, 3.35f, 0.022f, new Color(0.12f, 0.25f, 0.48f, 0.8f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 5.1f, 0.35f, Mathf.Sin(a) * 4f);
                Primitive(PrimitiveType.Cube, "StagePillar", root, p, new Vector3(0.14f, 0.7f, 0.14f), new Color(0.05f, 0.28f, 0.5f), true);
            }
            for (int i = 0; i < 5; i++)
            {
                float z = -3.5f + i * 1.75f;
                Ring(root, "GridLine", 0.01f, 0.01f, 0.012f, new Color(0.08f, 0.18f, 0.35f, 0.55f), new Vector3(0f, 0.11f, z), new Vector3(5.3f, 1f, 1f));
            }
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color color, bool emission)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var c = go.GetComponent<Collider>(); if (c != null) Object.Destroy(c);
            var r = go.GetComponent<Renderer>(); if (r != null) r.material = Material(color, emission);
            return go;
        }

        static void Ring(Transform parent, string name, float x, float z, float width, Color color)
        {
            Ring(parent, name, x, z, width, color, Vector3.zero, Vector3.one);
        }

        static void Ring(Transform parent, string name, float x, float z, float width, Color color, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.positionCount = 64; line.widthMultiplier = width;
            line.material = Material(color, true);
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * x, 0.12f, Mathf.Sin(a) * z));
            }
        }

        static Material Material(Color color, bool emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.8f);
            }
            return material;
        }
    }
}

