
using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace LAHEE {

    internal class StaticDataManager {

        private const String SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX = "1.3.0";

        private static Dictionary<int, GameData> gameData;

        public static void Initialize() {
            InitializeAchievements();

            Log.Data.LogInformation("Finished loading data: {games} Game(s) with {achiev} Achievements total", gameData.Count, gameData.Sum((r) => r.Value.Achievements.Length));
        }

        public static void InitializeAchievements() {
            gameData = new Dictionary<int, GameData>();

            string dir = Configuration.Get("LAHEE", "DataDirectory");
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
                Log.Data.LogTrace("Created directory");
            }

            Log.Data.LogInformation("Starting to read achievement data from {Dir}...", dir);

            foreach (string file in Directory.GetFiles(dir).Order()) {
                Log.Data.LogDebug("Detected file: {F}", file);
                try {
                    String fname = Path.GetFileNameWithoutExtension(file);
                    if (file.EndsWith(".json")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseAchievementJson(gameId, File.ReadAllText(file));
                    } else if (file.EndsWith(".txt")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseAchievementUserTxt(gameId, File.ReadAllLines(file));
                    } else if (file.EndsWith(".zzz")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseAchievementHashFile(gameId, File.ReadAllLines(file));
                    }
                } catch (Exception e) {
                    Log.Data.LogError("Error while reading data file {F}: {E}", file, e);
                }
            }
        }

        private static void ParseAchievementHashFile(int gameId, string[] strings) {

            if (!gameData.ContainsKey(gameId)) {
                Log.Data.LogError("Can't add hashes for game ID {ID}, game does not exist", gameId);
                return;
            }

            Log.Data.LogDebug("Starting to process hash file for game ID {ID}", gameId);
            GameData game = gameData[gameId];
            foreach (string str in strings) {
                game.ROMHashes.Add(str);
            }

            Log.Data.LogInformation("Added {n} ROM hashes to \"{game}\", total {n1}", strings.Length, game.Title, game.ROMHashes.Count);
        }

        private static void ParseAchievementUserTxt(int gameId, string[] content) {
            Log.Data.LogDebug("Starting to process user data file for game ID {ID}", gameId);

            if (!content[0].StartsWith(SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX)) {
                throw new Exception("Invalid local achievement file version: " + content[0]);
            }

            GameData data = new GameData() {
                ID = gameId,
                Title = content[1]
            };

            List<AchievementData> achievements = new List<AchievementData>();
            for (int i = 2; i < content.Length; i++) {

                String line = content[i];

                line = line.Replace("\\\"", "\"\""); // fix escape

                if (String.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                string[] parts;

                using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(line))) {
                    using (TextFieldParser tfp = new TextFieldParser(ms)) {
                        tfp.Delimiters = new string[] { ":" };
                        tfp.HasFieldsEnclosedInQuotes = true;
                        parts = tfp.ReadFields();
                    }
                }

                AchievementData a = new AchievementData() {
                    ID = Int32.Parse(parts[0]),
                    MemAddr = parts[1],
                    Title = parts[2],
                    Description = parts[3],
                    // 4 = progress
                    // 5 = max
                    Type = AchievementData.ConvertType(parts[6]),
                    Author = parts[7],
                    Points = Int32.Parse(parts[8]),
                    // 9 = created
                    // 10 = modified
                    // 11 = upvotes(?)
                    // 12 = downvotes(??)
                    BadgeName = parts[13],
                    BadgeURL = Network.LOCAL_URL + "/Badge/" + parts[13] + ".png",
                    BadgeLockedURL = Network.LOCAL_URL + "/Badge/" + parts[13] + "_lock.png",
                    Flags = 3, // TODO
                };

                achievements.Add(a);

            }
            data.Achievements = achievements.ToArray();

            RegisterOrMergeGame(gameId, data);
        }

        private static void ParseAchievementJson(int gameId, string content) {
            Log.Data.LogDebug("Starting to process official data file for game ID {ID}", gameId);
            GameData data = JsonConvert.DeserializeObject<GameData>(content);

            RegisterOrMergeGame(gameId, data);
        }

        private static int GetGameIdFromFilename(string fname) {
            int gameId = 0;
            if (!Int32.TryParse(fname.Split('-')[0], out gameId)) {
                Log.Data.LogWarning("No valid gameid found in filename: {F}", fname);
            }
            return gameId;
        }

        private static void RegisterOrMergeGame(int gameId, GameData game) {
            if (gameId == 0) {
                gameId = game.ID;
            } else {
                game.ID = gameId;
            }
            if (game.Title == null) {
                game.Title = "Unnamed Game " + gameId;
            }

            if (gameData.ContainsKey(gameId)) {
                MergeGame(game);
            } else {
                RegisterGame(game);
            }
        }

        private static void RegisterGame(GameData game) {
            gameData.Add(game.ID, game);
            Log.Data.LogInformation("Registered \"{Game}\" with {n} achievement(s)", game.Title, game.Achievements.Length);
        }

        private static void MergeGame(GameData game) {
            GameData existing = gameData[game.ID];

            if (game.RichPresencePatch != null) {
                Log.Data.LogWarning("While merging \"{Game}\" into \"{Game2}\", found two rich presence patches", game.Title, existing.Title);
                existing.RichPresencePatch = game.RichPresencePatch;
            }

            if (existing.Title == null || existing.Title.StartsWith("Unnamed")) {
                existing.Title = game.Title;
            }
            if (existing.ImageIcon == null) {
                existing.ImageIcon = game.ImageIcon;
            }
            if (existing.ImageIconURL == null) {
                existing.ImageIconURL = game.ImageIconURL;
            }
            if (existing.ConsoleID == 0) {
                existing.ConsoleID = game.ConsoleID;
            }

            existing.Achievements = existing.Achievements.Concat(game.Achievements).ToArray();

            Log.Data.LogInformation("Merged \"{Game}\" into \"{Game2}\" with {n} achievement(s), total {n2}", game.Title, existing.Title, game.Achievements.Length, existing.Achievements.Length);
        }

        public static GameData FindGameDataById(int id) {
            if (gameData.TryGetValue(id, out GameData value)) {
                return value;
            } else {
                return null;
            }
        }

        public static GameData FindGameDataByHash(String str) {
            return gameData.Where(r => r.Value.ROMHashes.Contains(str)).FirstOrDefault().Value;
        }

        public static GameData FindGameDataByName(string str, bool partial) {
            if (partial) {
                return gameData.Where(r => r.Value.Title.Contains(str)).FirstOrDefault().Value;
            } else {
                return gameData.Where(r => r.Value.Title.Equals(str)).FirstOrDefault().Value;
            }
        }
    }

}