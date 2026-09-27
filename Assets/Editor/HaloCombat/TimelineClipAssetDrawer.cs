using Combat.Config;
using Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor
{
    [CustomPropertyDrawer(typeof(TimelineClipAsset))]
    public sealed class TimelineClipAssetDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var kind = property.FindPropertyRelative("Kind");
            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                rect.y += EditorGUIUtility.singleLineHeight + 2f;
                Draw(rect, property, "Start");
                rect.y += EditorGUIUtility.singleLineHeight + 2f;
                Draw(rect, property, "End");
                rect.y += EditorGUIUtility.singleLineHeight + 2f;
                Draw(rect, property, "Kind");
                var value = (ClipKind)kind.enumValueIndex + 1;
                string[] fields = value == ClipKind.Move
                    ? new[] { "MoveX", "MoveY", "MoveZ", "Steer" }
                    : value == ClipKind.Hitbox
                        ? new[] { "HitRadius", "HitOffsetX", "HitOffsetY", "HitOffsetZ", "HitProfile" }
                        : System.Array.Empty<string>();
                foreach (var field in fields)
                {
                    rect.y += EditorGUIUtility.singleLineHeight + 2f;
                    Draw(rect, property, field);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
            var kind = property.FindPropertyRelative("Kind").enumValueIndex + 1;
            int count = kind == (int)ClipKind.Move ? 7 : kind == (int)ClipKind.Hitbox ? 8 : 3;
            return count * (EditorGUIUtility.singleLineHeight + 2f);
        }

        static void Draw(Rect rect, SerializedProperty owner, string name)
            => EditorGUI.PropertyField(rect, owner.FindPropertyRelative(name), true);
    }
}
