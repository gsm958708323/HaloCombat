using System;
using System.IO;
using Combat.Presentation;

namespace Combat.Game
{
    public sealed class GameSettings
    {
        public float MasterVolume = 1f;
        public bool ScreenShake = true,
            ShowHitboxes;

        public void Apply()
        {
            MasterVolume = Math.Max(0, Math.Min(1, MasterVolume));
            PresentSettings.ShowHitboxes = ShowHitboxes;
        }
    }

    public interface ISettingsStore
    {
        GameSettings Load();
        void Save(GameSettings s);
    }

    public sealed class MemorySettingsStore : ISettingsStore
    {
        GameSettings _s = new GameSettings();

        public GameSettings Load() => _s;

        public void Save(GameSettings s) => _s = s ?? new GameSettings();
    }

    public sealed class FileSettingsStore : ISettingsStore
    {
        readonly string _path;

        public FileSettingsStore(string p) =>
            _path = p ?? Path.Combine(Path.GetTempPath(), "halocombat-settings.txt");

        public GameSettings Load()
        {
            var s = new GameSettings();
            if (!File.Exists(_path))
                return s;
            try
            {
                foreach (var line in File.ReadAllLines(_path))
                {
                    var i = line.IndexOf('=');
                    if (i <= 0)
                        continue;
                    var k = line.Substring(0, i).Trim();
                    var v = line.Substring(i + 1).Trim();
                    if (k == "MasterVolume")
                        float.TryParse(
                            v,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out s.MasterVolume
                        );
                    else if (k == "ScreenShake")
                        s.ScreenShake =
                            v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                    else if (k == "ShowHitboxes")
                        s.ShowHitboxes =
                            v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return new GameSettings();
            }
            return s;
        }

        public void Save(GameSettings s)
        {
            if (s == null)
                return;
            File.WriteAllLines(
                _path,
                new[]
                {
                    "MasterVolume="
                        + s.MasterVolume.ToString(
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                    "ScreenShake=" + (s.ScreenShake ? "1" : "0"),
                    "ShowHitboxes=" + (s.ShowHitboxes ? "1" : "0"),
                }
            );
        }
    }
}
