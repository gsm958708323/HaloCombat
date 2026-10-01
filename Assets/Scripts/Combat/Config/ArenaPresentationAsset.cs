using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Arena Presentation")]
    public sealed class ArenaPresentationAsset : ScriptableObject
    {
        public float CameraDistance = 2.5f;
        public float CameraHeight = 2.5f;
        public float CameraBaseFov = 60f;
    }
}
