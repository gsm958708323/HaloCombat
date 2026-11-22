using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class SkillLabActorDisplay
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly Color FlashColor = new Color(.43f, 1f, .89f);
        readonly Renderer[] _renderers;
        readonly int _colorId;
        readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        float _previousFlash;

        public SkillLabActorDisplay(GameObject root)
        {
            _renderers = root.GetComponentsInChildren<Renderer>();
            _colorId = _renderers.Length > 0 && _renderers[0].sharedMaterial.HasProperty(BaseColor) ? BaseColor : LegacyColor;
        }

        public void Sample(float flash)
        {
            if (flash == _previousFlash) return;
            _previousFlash = flash;
            foreach (var renderer in _renderers)
            {
                if (flash <= 0) { renderer.SetPropertyBlock(null); continue; }
                _properties.SetColor(_colorId, Color.Lerp(renderer.sharedMaterial.color, FlashColor, flash * .85f));
                renderer.SetPropertyBlock(_properties);
            }
        }

        public static void BuildDummy(Transform root, Material wood, Material endGrain, Material band)
        {
            var plinth = Part(PrimitiveType.Cylinder, "Plinth", band, 0, .075f, 0, 1.05f, .075f, 1.05f);
            plinth.GetComponent<Renderer>().receiveShadows = true;
            Part(PrimitiveType.Cylinder, "Wooden post", wood, 0, .82f, 0, .60f, .72f, .60f);
            Part(PrimitiveType.Cylinder, "End grain", endGrain, 0, 1.556f, 0, .60f, .022f, .60f);
            Part(PrimitiveType.Cylinder, "Top binding", band, 0, 1.25f, 0, .632f, .046f, .632f);
            Part(PrimitiveType.Cylinder, "Lower binding", band, 0, .58f, 0, .632f, .046f, .632f);
            Part(PrimitiveType.Cylinder, "Crossbar", wood, 0, 1.02f, 0, .162f, .57f, .162f)
                .transform.localRotation = Quaternion.Euler(0, 0, 90);
            Part(PrimitiveType.Cube, "Target plate", endGrain, 0, .91f, -.291f, .37f, .4f, .06f);
            Part(PrimitiveType.Cube, "Target center", band, 0, .91f, -.326f, .15f, .15f, .02f)
                .transform.localRotation = Quaternion.Euler(0, 0, 45);

            GameObject Part(PrimitiveType type, string label, Material material, float x, float y, float z, float sx, float sy, float sz) =>
                SkillLabStage.Primitive(type, root, label, material, new Vector3(x, y, z), new Vector3(sx, sy, sz));
        }

        public static void BuildCaster(Transform root, Material cloth, Material armor, Material visor)
        {
            SkillLabStage.Primitive(PrimitiveType.Capsule, root, "Body", cloth, new Vector3(0, .65f, 0), new Vector3(.68f, .61f, .58f));
            SkillLabStage.Primitive(PrimitiveType.Cube, root, "Belt", armor, new Vector3(0, .55f, 0), new Vector3(.65f, .13f, .54f));
            SkillLabStage.Primitive(PrimitiveType.Sphere, root, "Helmet", armor, new Vector3(0, 1.19f, .01f), new Vector3(.45f, .43f, .48f));
            SkillLabStage.Primitive(PrimitiveType.Cube, root, "Visor", visor, new Vector3(0, 1.2f, .221f), new Vector3(.28f, .095f, .04f));
            SkillLabStage.Primitive(PrimitiveType.Cube, root, "Right gauntlet", armor, new Vector3(.365f, .82f, .17f), new Vector3(.14f, .22f, .35f));
            SkillLabStage.Primitive(PrimitiveType.Cube, root, "Left gauntlet", armor, new Vector3(-.365f, .82f, .17f), new Vector3(.14f, .22f, .35f));
        }

        public static float Scale(SkillActorSnapshot actor) => Mathf.Clamp(actor.Radius / .42f, .72f, 1.65f);
    }
}
