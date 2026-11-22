using UnityEngine;

namespace Combat.Unity
{
    internal static class CombatGroundOutline
    {
        const int Segments = 64;

        public static LineRenderer Create(Transform parent, Material material, string name)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            var line = visual.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.sharedMaterial = material;
            line.widthMultiplier = .035f;
            return line;
        }

        public static void SetRadius(LineRenderer line, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, .07f, Mathf.Sin(angle) * radius));
            }
        }
    }
}
