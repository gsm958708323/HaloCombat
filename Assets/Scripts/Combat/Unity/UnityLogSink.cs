using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityLogSink : ILogSink
    {
        readonly Object _context;

        public UnityLogSink(Object context = null)
        {
            _context = context;
        }

        public void Write(LogRecord record)
        {
            string text = Format(record);
            switch (record.Level)
            {
                case LogLevel.Error:
                    Debug.LogError(text, _context);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning(text, _context);
                    break;
                default:
                    Debug.Log(text, _context);
                    break;
            }
        }

        static string Format(LogRecord record)
        {
            string text = "[" + record.Level + "][<color=" + CategoryColor(record.Category) + ">" +
                          record.Category + "</color>] " + record.Message;
            if (record.Exception != null)
                text += System.Environment.NewLine + record.Exception;
            return text;
        }

        static string CategoryColor(string category)
        {
            switch (category)
            {
                case CombatCategories.TagInput: return "#4FC3F7";
                case CombatCategories.Attribute: return "#81C784";
                case CombatCategories.Buff: return "#FFB74D";
                case CombatCategories.ActivityMotor: return "#BA68C8";
                case CombatCategories.ClipPayload: return "#64B5F6";
                case CombatCategories.MeleeDamage: return "#E57373";
                case CombatCategories.ProjectileAoe: return "#4DB6AC";
                case CombatCategories.SeasonOne: return "#FFD54F";
                default: return "#B0BEC5";
            }
        }
    }
}
