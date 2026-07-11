using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace REPO_Instant_Save.Save
{
    /// <summary>
    /// Reads/writes the Instant Save snapshot as JSON, co-located with the run's native
    /// .es3 file (one snapshot per save folder) so it persists across game restarts.
    /// </summary>
    internal static class InstantSaveStore
    {
        private const string FileName = "instantsave.json";

        public static string FolderFor(string saveName) =>
            Path.Combine(Application.persistentDataPath, "saves", saveName);

        public static string PathFor(string saveName) =>
            Path.Combine(FolderFor(saveName), FileName);

        public static void Write(string saveName, WorldSnapshot snapshot)
        {
            string folder = FolderFor(saveName);
            Directory.CreateDirectory(folder);
            string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(PathFor(saveName), json);
        }

        public static WorldSnapshot? Read(string saveName)
        {
            string path = PathFor(saveName);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<WorldSnapshot>(File.ReadAllText(path));
        }

        public static bool Exists(string saveName) => File.Exists(PathFor(saveName));
    }
}
