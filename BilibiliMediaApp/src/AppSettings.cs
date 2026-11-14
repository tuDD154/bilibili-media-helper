using System;
using System.IO;
using System.Web.Script.Serialization;

namespace BilibiliMediaApp
{
    public sealed class AppSettings
    {
        public int SeekSeconds { get; set; }
        public string LastUrl { get; set; }
        public string HotkeyToggle { get; set; }
        public string HotkeyBack { get; set; }
        public string HotkeyForward { get; set; }
        public bool TopMost { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public bool HasWindowBounds { get; set; }

        private static string _dataDirectory;

        public AppSettings()
        {
            SeekSeconds = 3;
            LastUrl = "https://www.bilibili.com/";
            HotkeyToggle = HotkeyBinding.DefaultToggle.ToStorageString();
            HotkeyBack = HotkeyBinding.DefaultBack.ToStorageString();
            HotkeyForward = HotkeyBinding.DefaultForward.ToStorageString();
            TopMost = false;
            WindowWidth = 1440;
            WindowHeight = 820;
            WindowX = 80;
            WindowY = 60;
            HasWindowBounds = false;
        }

        public static string DataDirectory
        {
            get
            {
                if (!string.IsNullOrEmpty(_dataDirectory))
                {
                    return _dataDirectory;
                }
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            }
        }

        public static void SetDataDirectory(string directory)
        {
            if (!string.IsNullOrEmpty(directory))
            {
                _dataDirectory = Path.GetFullPath(directory);
            }
        }

        public static string ResolveDefaultDataDirectory()
        {
            string portable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (IsWritableDirectory(portable))
            {
                return portable;
            }

            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BilibiliMediaApp");
            if (IsWritableDirectory(local))
            {
                return local;
            }

            string temp = Path.Combine(Path.GetTempPath(), "BilibiliMediaApp");
            Directory.CreateDirectory(temp);
            return temp;
        }

        private static bool IsWritableDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(DataDirectory, "settings.json"); }
        }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsPath);
                var serializer = new JavaScriptSerializer();
                AppSettings loaded = serializer.Deserialize<AppSettings>(json);
                if (loaded == null)
                {
                    return new AppSettings();
                }
                loaded.Normalize();
                return loaded;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Normalize()
        {
            if (SeekSeconds < 1 || SeekSeconds > 30)
            {
                SeekSeconds = 3;
            }
            if (string.IsNullOrEmpty(LastUrl) ||
                (!LastUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                 !LastUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                LastUrl = "https://www.bilibili.com/";
            }
            if (WindowWidth <= 0 || WindowWidth > 5000)
            {
                WindowWidth = 1440;
            }
            if (WindowHeight <= 0 || WindowHeight > 4000)
            {
                WindowHeight = 820;
            }

            HotkeyBinding toggle = HotkeyBinding.FromStorage(
                HotkeyToggle, HotkeyBinding.DefaultToggle);
            HotkeyBinding back = HotkeyBinding.FromStorage(
                HotkeyBack, HotkeyBinding.DefaultBack);
            HotkeyBinding forward = HotkeyBinding.FromStorage(
                HotkeyForward, HotkeyBinding.DefaultForward);

            if (toggle.SameKey(back) || toggle.SameKey(forward) || back.SameKey(forward))
            {
                toggle = HotkeyBinding.DefaultToggle;
                back = HotkeyBinding.DefaultBack;
                forward = HotkeyBinding.DefaultForward;
            }

            HotkeyToggle = toggle.ToStorageString();
            HotkeyBack = back.ToStorageString();
            HotkeyForward = forward.ToStorageString();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Settings are a convenience; never crash because of a write failure.
            }
        }
    }
}
