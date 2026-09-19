using UnityEngine;

namespace Combat.Unity.Presentation
{
    public static class ProceduralVfxFactory
    {
        public static GameObject Create(Transform root, string key, Vector3 pos)
        {
            var go = new GameObject("Vfx_" + key);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var lower = (key ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("slash") || lower.Contains("g1") || lower.Contains("g2"))
            {
                var arc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arc.transform.SetParent(go.transform, false);
                arc.transform.localScale = new Vector3(1.15f, .025f, 1.15f);
                arc.transform.localRotation = Quaternion.Euler(90, 0, 0);
                arc.GetComponent<Renderer>().material.color = new Color(1f, .82f, .2f, .85f);
                Object.Destroy(arc.GetComponent<Collider>());
            }
            else
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ring.transform.SetParent(go.transform, false);
                ring.transform.localScale = Vector3.one * .35f;
                ring.GetComponent<Renderer>().material.color = lower.Contains("ground")
                    ? new Color(1f, .35f, .08f)
                    : new Color(1f, .9f, .45f);
                Object.Destroy(ring.GetComponent<Collider>());
            }
            return go;
        }

        public static GameObject CreateLoop(Transform root, int visualId)
        {
            var go = new GameObject("LoopVfx_" + visualId);
            go.transform.SetParent(root, false);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0, .05f, 0);
            ring.transform.localScale = new Vector3(.8f, .015f, .8f);
            ring.GetComponent<Renderer>().material.color = new Color(.2f, .85f, 1f, .45f);
            Object.Destroy(ring.GetComponent<Collider>());
            return go;
        }
    }
}
