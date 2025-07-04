﻿
using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System.Text;

namespace LAHEE {
    static class StaticDataManager {
        
        private static readonly String[] SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX_LIST = new String[]{"1.3.0", "1.3.1"};
        public const int UNSUPPORTED_EMULATOR_ACHIEVEMENT_ID = 101000001;

        private static Dictionary<int, GameData> gameData;
        private static Dictionary<int, List<UserComment>> commentData;

        public static void Initialize() {
            InitializeAchievements();

            Log.Data.LogInformation("Finished loading data: {games} Game(s) with {achiev} Achievements total", gameData.Count, gameData.Sum((r) => r.Value.Achievements.Length));
        }

        public static void InitializeAchievements() {
            gameData = new Dictionary<int, GameData>();
            commentData = new Dictionary<int, List<UserComment>>();

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
                    if (file.EndsWith("comments.json")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseCommentDataJson(gameId, File.ReadAllText(file));
                    } else if (file.EndsWith(".json")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseAchievementJson(gameId, File.ReadAllText(file));
                    } else if (file.EndsWith(".txt")) {
                        int gameId = GetGameIdFromFilename(fname);
                        ParseAchievementUserTxt(gameId, File.ReadAllLines(file));
                    } else if (file.EndsWith(".zzz") || file.EndsWith(".zhash")) {
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

            if (!SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX_LIST.Any(v => content[0].StartsWith(v))) {
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
                    BadgeURL = "/Badge/" + parts[13] + ".png",
                    BadgeLockedURL = "/Badge/" + parts[13] + "_lock.png",
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

        private static int GetGameIdFromFilename(string fileName) {
            if (!Int32.TryParse(fileName.Split('-')[0], out int gameId)) {
                Log.Data.LogWarning("No valid game id found in filename: {F}", fileName);
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
                if (existing.RichPresencePatch != null) {
                    Log.Data.LogWarning("While merging \"{Game}\" into \"{Game2}\", found two rich presence patches", game.Title, existing.Title);
                }
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
            return gameData.FirstOrDefault(r => r.Value.ROMHashes.Contains(str)).Value;
        }

        public static GameData FindGameDataByName(string str, bool partial) {
            if (partial) {
                return gameData.FirstOrDefault(r => r.Value.Title.Contains(str)).Value;
            } else {
                return gameData.FirstOrDefault(r => r.Value.Title.Equals(str)).Value;
            }
        }

        internal static GameData[] GetAllGameData() {
            return gameData.Values.ToArray();
        }

        public static string LocalifyUrl(string url) {
            return url
                .Replace("https://media.retroachievements.org", "")
                .Replace("https://retroachievements.org", "")
                .Replace("/Images/", "/Badge/")
                ;
        }
        
        private static void ParseCommentDataJson(int gameId, string content) {
            Log.Data.LogDebug("Starting to process comment data file for game ID {ID}", gameId);
            List<UserComment> data = JsonConvert.DeserializeObject<List<UserComment>>(content);
            commentData[gameId] = data;
        }

        public static List<UserComment> FindCommentDataByGameId(int gameId) {
            if (commentData.TryGetValue(gameId, out List<UserComment> value)) {
                return value;
            } else {
                return null;
            }
        }

        internal static UserComment[] GetAllUserComments() {
            List<UserComment> list = new List<UserComment>();
            foreach (List<UserComment> gameList in commentData.Values) {
                list.AddRange(gameList);
            }

            return list.ToArray();
        }

        public static void AddComment(UserComment comment, GameData game, bool saveData = true) {
            List<UserComment> comments = FindCommentDataByGameId(game.ID);
            if (comments == null) {
                Log.Data.LogDebug("Created comment object for game {ID}", game.ID);
                comments = new List<UserComment>();
                commentData[game.ID] = comments;
            }

            if (!comments.Any(c => c.Submitted.Equals(comment.Submitted) && c.ULID.Equals(comment.ULID))) {
                comments.Add(comment);
                Log.Data.LogInformation("Added comment from {u} for game {g}", comment.User, game.Title);
            }

            if (saveData) {
                SaveCommentFile(game);
            }
        }

        public static void AddComment(UserData userData, GameData game, AchievementData ach, string comment, bool saveData = true) {
            AddComment(new UserComment() {
                User = userData.UserName,
                Submitted = DateTime.Now.ToUniversalTime(),
                ULID = "LAHEE" + userData.ID + "-" + game.ID + "-" + DateTime.Now.ToString("s"),
                CommentText = comment,
                AchievementID = ach.ID,
                IsLocal = true,
                LaheeUUID = Guid.NewGuid()
            }, game, saveData);
        }

        public static void SaveCommentFile(GameData game) {
            String fileBase = Configuration.Get("LAHEE", "DataDirectory") + "\\" + game.ID + "-" + new string(game.Title.Where(ch => !Program.INVALID_FILE_NAME_CHARS.Contains(ch)).ToArray());
            String fileData = fileBase + "-comments.json";

            List<UserComment> comments = FindCommentDataByGameId(game.ID);
            
            File.WriteAllText(fileData, JsonConvert.SerializeObject(comments));
            Log.Data.LogInformation("Comment data was saved for " + game);
            
        }

        public static bool DeleteComment(GameData game, string uuidString) {
            Guid uuid = Guid.Parse(uuidString);
            List<UserComment> comments = commentData[game.ID];
            if (comments != null) {
                UserComment c = comments.FirstOrDefault(c => c.LaheeUUID.Equals(uuid));
                if (c != null) {
                    comments.Remove(c);
                    return true;
                }
            }

            return false;
        }

        public static void SaveAllCommentFiles() {
            foreach (int id in commentData.Keys) {
                GameData game = FindGameDataById(id);
                if (game != null) {
                    SaveCommentFile(game);
                }
            }
        }
    }

}