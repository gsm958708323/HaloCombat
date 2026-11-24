using System;
using System.Collections.Generic;
using System.IO;

namespace Combat.Config
{
    public static class PublishedSkillSlots
    {
        // The original filenames are part of the user's published slot storage.
        public static IReadOnlyList<string> Ordered { get; } = Array.AsReadOnly(new[] { "f6", "f7", "slot3", "slot4" });

        // Readers that load a group share the writer's lock so they cannot see a partial sync.
        public static IReadOnlyDictionary<string, string> Read(string directory) =>
            Locked(directory, () => ReadUnlocked(directory));

        public static IReadOnlyDictionary<string, string> ReadArenaSlots(string directory) =>
            Locked(directory, () => ReadUnlocked(directory, 2));

        internal static T Locked<T>(string directory, Func<T> action) =>
            ProjectFileTransaction.Locked(new DirectoryInfo(directory).FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar, action);

        internal static Dictionary<string, string> ReadUnlocked(string directory, int count = 4)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                string slot = Ordered[index];
                string path = PathOf(directory, slot);
                if (File.Exists(path)) result.Add(slot, File.ReadAllText(path));
            }
            return result;
        }

        internal static string PathOf(string directory, string slot) => Path.Combine(directory, slot + ".json");
    }
}
