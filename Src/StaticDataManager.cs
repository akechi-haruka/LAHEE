using System.Text;
using LAHEE.Data;
using LAHEE.Data.File;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;

namespace LAHEE;

static class StaticDataManager {
    private static readonly String[] SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX_LIST = new String[] { "1.3.0", "1.3.1", "1.4.0" };
    private const String CUSTOM_ACHIEVEMENT_COUNTER_FILE = "lahee_custom_achievement_counter.txt";
    public const int UNSUPPORTED_EMULATOR_ACHIEVEMENT_ID = 101000001;

    private enum LoadPriority {
        GameDataV1,
        GameDataV1Local,
        GameDataV2,
        AchievementData,
        Comments,
        Hash
    }

    private static Dictionary<uint, GameData> gameData;
    private static Dictionary<uint, List<UserComment>> commentData;
    private static int customAchievementIdNext = 5_000_000;

    public static void Initialize() {
        InitializeAchievements(true);

        Log.Data.LogInformation("Finished loading data: {games} Game(s) with {achiev} Achievements total", gameData.Count, gameData.Sum(r => r.Value.GetAchievementCount()));
    }

    public static String GetDirectory() {
        return Configuration.Get("LAHEE", "DataDirectory");
    }

    public static void InitializeAchievements(bool initial = false) {
        gameData = new Dictionary<uint, GameData>();
        commentData = new Dictionary<uint, List<UserComment>>();

        string dir = GetDirectory() ?? "Data";
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
            Log.Data.LogTrace("Created directory");
        }

        Log.Data.LogInformation(initial ? "Starting to read achievement data from {Dir}..." : "Reloading achievement data from {Dir}...", dir);

        Dictionary<LoadPriority, List<string>> loadQueue = Enum.GetValues<LoadPriority>().ToDictionary(p => p, _ => new List<string>());

        foreach (string file in Directory.GetFiles(dir).Order()) {
            Log.Data.LogDebug("Detected file: {F}", file);
            if (file.EndsWith(CUSTOM_ACHIEVEMENT_COUNTER_FILE)) {
                ParseAchievementCounterFile(File.ReadAllText(file));
            } else if (file.EndsWith("comments.json")) {
                loadQueue[LoadPriority.Comments].Add(file);
            } else if (file.EndsWith(".ach.json")) {
                loadQueue[LoadPriority.AchievementData].Add(file);
            } else if (file.EndsWith(".set.json")) {
                loadQueue[LoadPriority.GameDataV2].Add(file);
            } else if (file.EndsWith(".json")) {
                loadQueue[LoadPriority.GameDataV1].Add(file);
            } else if (file.EndsWith(".zzz") || file.EndsWith(".zhash") || file.EndsWith(".hash.txt")) {
                loadQueue[LoadPriority.Hash].Add(file);
            } else if (file.EndsWith(".txt")) {
                loadQueue[LoadPriority.GameDataV1Local].Add(file);
            }
        }

        loadQueue[LoadPriority.GameDataV1].ForEach(file => Process(file, ParseAchievementJson));
        loadQueue[LoadPriority.GameDataV1Local].ForEach(file => Process(file, ParseAchievementUserTxt));
        loadQueue[LoadPriority.GameDataV2].ForEach(file => Process(file, ParseSetAchievementJson));
        loadQueue[LoadPriority.AchievementData].ForEach(file => Process(file, ParseSingleAchievementJson));
        loadQueue[LoadPriority.Comments].ForEach(file => Process(file, ParseCommentDataJson));
        loadQueue[LoadPriority.Hash].ForEach(file => Process(file, ParseAchievementHashFile));
    }

    private static void Process(string file, Action<uint, string, string> parser) {
        try {
            uint gameId = GetGameIdFromFilePath(file);
            parser(gameId, File.ReadAllText(file), file);
        } catch (Exception e) {
            Log.Data.LogError("Error while reading data file {F}: {E}", file, e);
        }
    }

    private static void ParseAchievementHashFile(uint gameId, string filecontent, string file) {
        string[] strings = filecontent.Split(new String[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

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

    private static void ParseAchievementUserTxt(uint gameId, string filecontent, string source) {
        Log.Data.LogDebug("Starting to process user data file for game ID {ID}", gameId);
        string[] content = filecontent.Split(new String[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (!SUPPORTED_LOCAL_ACHIEVEMENT_FILE_VERSION_PREFIX_LIST.Any(v => content[0].StartsWith(v))) {
            throw new Exception("Invalid local achievement file version: " + content[0]);
        }

        GameDataJsonV1 data = new GameDataJsonV1() {
            ID = gameId,
            Title = content[1],
            SourceFilePath = source
        };

        List<AchievementData> achievements = new List<AchievementData>();
        for (int i = 2; i < content.Length; i++) {
            String line = content[i];

            line = line.Replace("\\\"", "\"\""); // fix escape

            if (String.IsNullOrWhiteSpace(line)) {
                continue;
            }

            string[] parts;

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(line))) {
                using (TextFieldParser tfp = new TextFieldParser(ms)) {
                    tfp.Delimiters = new string[] { ":" };
                    tfp.HasFieldsEnclosedInQuotes = true;
                    parts = tfp.ReadFields();
                }
            }

            if (parts == null) {
                throw new IOException("line read error: " + line);
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
                Flags = AchievementFlags.Always | AchievementFlags.Official,
            };

            achievements.Add(a);
        }

        data.Achievements = achievements;

        RegisterOrMergeGame(gameId, data);
    }

    private static void ParseSetAchievementJson(uint gameId, string content, string source) {
        Log.Data.LogDebug("Starting to process official data file for game ID {ID}", gameId);
        GameData game = JsonConvert.DeserializeObject<GameData>(content);

        RegisterOrMergeGame(gameId, game);
    }

    private static void ParseAchievementJson(uint gameId, string content, string source) {
        Log.Data.LogDebug("Starting to process official data file (v1) for game ID {ID}", gameId);
        GameDataJsonV1 legacy = JsonConvert.DeserializeObject<GameDataJsonV1>(content);

        RegisterOrMergeGame(gameId, legacy);
    }

    private static void ParseSingleAchievementJson(uint gameId, string content, string source) {
        Log.Data.LogDebug("Starting to process single achievement file for game ID {ID}", gameId);

        if (!gameData.TryGetValue(gameId, out GameData game)) {
            Log.Data.LogError("Can't add single achievement for game ID {ID}, game does not exist", gameId);
            return;
        }

        AchievementData data = JsonConvert.DeserializeObject<AchievementData>(content);
        SetData set = new SetData() {
            Achievements = new List<AchievementData>() {
                data
            },
            Leaderboards = new List<LeaderboardData>(),
            Title = "Single: " + data.Title,
            AchievementSetId = game.AchievementSets.Count + 1,
            FileSource = source,
            GameId = RAIntegrationAssertionWorkaround(game.ID),
            ImageIconUrl = game.ImageIconURL,
            Type = SetType.core
        };

        game.AchievementSets.Add(set);
        Log.Data.LogInformation("Registered single achievement \"{T}\" for \"{Game}\"", data.Title, game.Title);
    }

    public static uint RAIntegrationAssertionWorkaround(uint gameId) {
        /*
            Unexpected error
            Assertion failure at src\data\context\GameContext.cpp: 563
            ??????????????????????????????????????
         */
        return ~gameId;
    }

    public static uint GetGameIdFromFilePath(string filePath) {
        if (!UInt32.TryParse(Path.GetFileName(filePath).Split('-')[0], out uint gameId)) {
            Log.Data.LogWarning("No valid game id found in path: {F}", filePath);
        }

        return gameId;
    }

    private static void RegisterOrMergeGame(uint gameId, GameDataJsonV1 legacy) {
        GameData game = new GameData(legacy);
        game.Upgrade();
        RegisterOrMergeGame(gameId, game);
    }

    private static void RegisterOrMergeGame(uint gameId, GameData game) {
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
        Log.Data.LogInformation("Registered \"{Game}\" with {n} achievement(s)", game.Title, game.GetAchievementCount());
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

        existing.AchievementSets.ForEach(set => {
            int removed = set.Achievements.RemoveAll(a => game.GetAchievementById(a.ID) != null);
            if (removed > 0) {
                Log.Data.LogWarning("Removed {n} duplicate achievement(s)", removed);
            }
        });
        existing.AchievementSets.AddRange(game.AchievementSets);

        Log.Data.LogInformation("Merged \"{Game}\" into \"{Game2}\" with {n} achievement(s), total {n2}", game.Title, existing.Title, game.GetAchievementCount(), existing.GetAchievementCount());
    }

    public static GameData FindGameDataById(uint id) {
        return gameData.GetValueOrDefault(id) ?? gameData.GetValueOrDefault(RAIntegrationAssertionWorkaround(id));
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

    internal static List<GameData> GetAllGameData() {
        return gameData.Values.ToList();
    }

    [Obsolete]
    internal static List<GameDataJsonV1> GetAllGameDataAsV1() {
        return gameData.Values.Select(data => new GameDataJsonV1(data)).ToList();
    }

    public static string LocalifyUrl(string url) {
        return url
                .Replace("https://media.retroachievements.org", "")
                .Replace("https://retroachievements.org", "")
                .Replace("/Images/", "/Badge/")
            ;
    }

    private static void ParseCommentDataJson(uint gameId, string content, string file) {
        Log.Data.LogDebug("Starting to process comment data file for game ID {ID}", gameId);
        List<UserComment> data = JsonConvert.DeserializeObject<List<UserComment>>(content);
        if (data == null) {
            data = new List<UserComment>();
        }

        commentData[gameId] = data;
    }

    public static List<UserComment> FindCommentDataByGameId(uint gameId) {
        return commentData.GetValueOrDefault(gameId, null);
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
        foreach (GameData game in commentData.Keys.Select(FindGameDataById).Where(game => game != null)) {
            SaveCommentFile(game);
        }
    }

    private static void ParseAchievementCounterFile(String content) {
        customAchievementIdNext = Int32.Parse(content);
        Log.Data.LogDebug("Achievement counter initialized to: {c}", customAchievementIdNext);
    }

    public static int AssignNextCustomAchievementId() {
        int value = customAchievementIdNext++;
        String fn = Path.Combine(GetDirectory(), CUSTOM_ACHIEVEMENT_COUNTER_FILE);
        Log.Data.LogDebug("Writing next custom achievement ID ({id}) to {f}", customAchievementIdNext, fn);
        File.WriteAllText(fn, customAchievementIdNext.ToString());
        return value;
    }

    public static void SaveSingleAchievement(GameData game, AchievementData ach) {
        String fn = Path.Combine(Configuration.Get("LAHEE", "DataDirectory"), game.ID + "-z" + ach.ID + "-" + new string(game.Title.Where(ch => !Program.INVALID_FILE_NAME_CHARS.Contains(ch)).ToArray()) + "-" + new string(ach.Title.Where(ch => !Program.INVALID_FILE_NAME_CHARS.Contains(ch)).ToArray()) + ".ach.json");

        File.WriteAllText(fn, JsonConvert.SerializeObject(ach));
        Log.Data.LogInformation("Achievement data was saved for {a}", ach);
    }
}