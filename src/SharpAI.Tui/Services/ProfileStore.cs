namespace SharpAI.Tui.Services
{
    using System;
    using System.IO;
    using System.Text.Json;

    internal sealed class ProfileStore
    {
        private readonly string _Filename;
        internal ProfileStore(string? directory = null)
        {
            _Filename = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpai"), "tui.json");
        }
        internal ConnectionProfile Load()
        {
            return File.Exists(_Filename) ? JsonSerializer.Deserialize<ConnectionProfile>(File.ReadAllText(_Filename)) ?? new ConnectionProfile() : new ConnectionProfile();
        }
        internal void Save(ConnectionProfile profile)
        {
            string directory = Path.GetDirectoryName(_Filename)!;
            Directory.CreateDirectory(directory);
            string temporary = _Filename + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, _Filename, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
