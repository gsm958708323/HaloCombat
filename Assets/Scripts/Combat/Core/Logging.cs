using System;
using System.Threading;

namespace Combat.Core
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Off = 4
    }

    public readonly struct LogRecord
    {
        public readonly LogLevel Level;
        public readonly string Category;
        public readonly string Message;
        public readonly Exception Exception;

        public LogRecord(LogLevel level, string category, string message, Exception exception = null)
        {
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }

    public interface ILogSink
    {
        void Write(LogRecord record);
    }

    public static class CombatCategories
    {
        public const string TagInput = "TagInput";
        public const string Attribute = "Attribute";
        public const string Buff = "Buff";
        public const string ActivityMotor = "ActivityMotor";
        public const string ClipPayload = "ClipPayload";
        public const string MeleeDamage = "MeleeDamage";
        public const string ProjectileAoe = "ProjectileAoe";
        public const string SeasonOne = "SeasonOne";
        public const string Knockdown = "Knockdown";
        public const string DodgeHitstop = "DodgeHitstop";
        public const string AuraHoming = "AuraHoming";
        public const string BehaviorTree = "BehaviorTree";
        public const string Perception = "Perception";
        public const string EnemyAi = "EnemyAi";
        public const string Summon = "Summon";
        public const string SeasonTwo = "SeasonTwo";

        static readonly string[] All =
        {
            TagInput,
            Attribute,
            Buff,
            ActivityMotor,
            ClipPayload,
            MeleeDamage,
            ProjectileAoe,
            SeasonOne,
            Knockdown,
            DodgeHitstop,
            AuraHoming,
            BehaviorTree,
            Perception,
            EnemyAi,
            Summon,
            SeasonTwo
        };

        public static bool IsKnown(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return false;

            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], category.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string Canonicalize(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return string.Empty;

            string trimmed = category.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return All[i];
            }

            return string.Empty;
        }
    }

    public sealed class ConsoleLogSink : ILogSink
    {
        public void Write(LogRecord record)
        {
            Console.WriteLine(CombatLog.Format(record));
        }
    }

    public static class CombatLog
    {
        const string DefaultCategory = "Combat";
        static ILogSink _sink = NullLogSink.Instance;
        static int _minimumLevel = (int)LogLevel.Debug;
        static string _categoryFilter = string.Empty;

        public static LogLevel MinimumLevel
        {
            get { return (LogLevel)Volatile.Read(ref _minimumLevel); }
            set { Volatile.Write(ref _minimumLevel, (int)value); }
        }

        public static string CategoryFilter
        {
            get { return Volatile.Read(ref _categoryFilter); }
        }

        public static void SetSink(ILogSink sink)
        {
            Interlocked.Exchange(ref _sink, sink ?? NullLogSink.Instance);
        }

        public static void SetCategoryFilter(string category)
        {
            Volatile.Write(ref _categoryFilter, NormalizeCategory(category));
        }

        public static bool IsEnabled(LogLevel level)
        {
            return level >= MinimumLevel && level != LogLevel.Off;
        }

        public static bool IsEnabled(LogLevel level, string category)
        {
            if (!IsEnabled(level))
                return false;

            string filter = CategoryFilter;
            return string.IsNullOrEmpty(filter) ||
                   string.Equals(filter, category ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public static void Debug(string category, string message)
        {
            Write(new LogRecord(LogLevel.Debug, category, message));
        }

        public static void Info(string category, string message)
        {
            Write(new LogRecord(LogLevel.Info, category, message));
        }

        public static void Warn(string category, string message, Exception exception = null)
        {
            Write(new LogRecord(LogLevel.Warn, category, message, exception));
        }

        public static void Error(string category, string message, Exception exception = null)
        {
            Write(new LogRecord(LogLevel.Error, category, message, exception));
        }

        public static void Debug(string message)
        {
            Debug(DefaultCategory, message);
        }

        public static void Info(string message)
        {
            Info(DefaultCategory, message);
        }

        public static void Warn(string message, Exception exception = null)
        {
            Warn(DefaultCategory, message, exception);
        }

        public static void Error(string message, Exception exception = null)
        {
            Error(DefaultCategory, message, exception);
        }

        internal static void Write(LogRecord record)
        {
            if (!IsEnabled(record.Level, record.Category))
                return;

            try
            {
                Volatile.Read(ref _sink).Write(record);
            }
            catch
            {
                // Logging must never interrupt combat execution.
            }
        }

        public static string Format(LogRecord record)
        {
            string text = "[" + record.Level + "][" + (record.Category ?? string.Empty) + "] " + (record.Message ?? string.Empty);
            if (record.Exception != null)
                text += Environment.NewLine + record.Exception;
            return text;
        }

        static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category) ||
                string.Equals(category.Trim(), "All", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return category.Trim();
        }

        sealed class NullLogSink : ILogSink
        {
            public static readonly NullLogSink Instance = new NullLogSink();

            public void Write(LogRecord record)
            {
            }
        }
    }
}
