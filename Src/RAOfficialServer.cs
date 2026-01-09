using System.Net;
using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LAHEE;

public static class RAOfficialServer {
    private const string SERVER_ACCOUNT_USER_ID = "019Z8BMP7E37YNRVDSP8SV266G";

    public static string Url {
        get { return Program.Config.Get("LAHEE", "RAFetch", "Url"); }
    }

    public static string SessionToken { get; private set; }

    public static bool CanFetch {
        get {
            string apiWeb = Program.Config.Get("LAHEE", "RAFetch", "WebApiKey");
            string username = Program.Config.Get("LAHEE", "RAFetch", "Username");
            string password = Program.Config.Get("LAHEE", "RAFetch", "Password");

            if (String.IsNullOrWhiteSpace(Url)) {
                Log.Main.LogError("Invalid RAFetch Url in configuration.");
                return false;
            }

            if (String.IsNullOrWhiteSpace(apiWeb)) {
                Log.Main.LogError("Invalid RAFetch WebApiKey in configuration. Get it from here: {u}", Url + "/settings");
                return false;
            }

            if (String.IsNullOrWhiteSpace(username)) {
                Log.Main.LogError("Invalid RAFetch username in configuration.");
                return false;
            }

            if (String.IsNullOrWhiteSpace(password)) {
                Log.Main.LogError("Invalid RAFetch password in configuration.");
                return false;
            }

            return true;
        }
    }

    public static void FetchData(string gameIdStr, string overrideIdStr, bool includeUnofficial, string copyToUsername = null) {
        if (!CanFetch) {
            return;
        }

        string apiWeb = Program.Config.Get("LAHEE", "RAFetch", "WebApiKey");
        string username = Program.Config.Get("LAHEE", "RAFetch", "Username");

        if (!UInt32.TryParse(gameIdStr, out uint fetchId)) {
            Log.Main.LogError("Not a valid game ID: {i}", gameIdStr);
            return;
        }

        uint overrideId = fetchId;
        if (!String.IsNullOrWhiteSpace(overrideIdStr)) {
            if (!UInt32.TryParse(overrideIdStr, out overrideId)) {
                Log.Main.LogError("Not a valid game ID (override ID): {i}", overrideIdStr);
                return;
            }
        }

        string sessionToken = LogInToRealServer();
        if (sessionToken == null) {
            return;
        }

        int f = includeUnofficial ? 7 : 3;
        RAPatchResponseV2 patch = Query<RAPatchResponseV2>(HttpMethod.Get, Url, "dorequest.php?r=achievementsets&u=" + username + "&t=" + sessionToken + "&f=" + f + "&m=&g=" + fetchId, null);
        if (patch == null) {
            return;
        }

        GameData gameData = new GameData() {
            Title = patch.Title,
            ID = patch.GameId,
            ConsoleID = patch.ConsoleId,
            ImageIconURL = patch.ImageIconUrl,
            DataVersion = GameData.CURRENT_DATA_VERSION,
            ImageIcon = patch.ImageIconUrl,
            CodeNotes = new List<CodeNote>(),
            ROMHashes = new List<string>(),
            RichPresencePatch = patch.RichPresencePatch,
            AchievementSets = patch.Sets
        };

        List<string> hashes = new List<string>();

        RAApiHashesResponse hashResponse = Query<RAApiHashesResponse>(HttpMethod.Get, Url, "API/API_GetGameHashes.php?y=" + apiWeb + "&i=" + fetchId, null);
        if (hashResponse == null) {
            return;
        }

        foreach (RAApiHashesResponse.Hash h in hashResponse.Results) {
            if (!hashes.Contains(h.MD5)) {
                hashes.Add(h.MD5);
            }
        }

        Dictionary<string, string> imageDownloads = new Dictionary<string, string>();
        imageDownloads.Add(gameData.ImageIcon, gameData.ImageIconURL);
        foreach (AchievementData ad in gameData.GetAllAchievements()) {
            imageDownloads[ad.BadgeName] = ad.BadgeURL;
            imageDownloads[Path.GetFileNameWithoutExtension(ad.BadgeName) + "_lock.png"] = ad.BadgeLockedURL;
        }

        FetchCodeNotes(gameData);

        // Achievement data modifications:

        // apply overridden ID for set merges
        gameData.ID = overrideId;
        // re-route image URLs to local
        gameData.ImageIconURL = StaticDataManager.LocalifyUrl(gameData.ImageIconURL);
        foreach (AchievementData ad in gameData.GetAllAchievements()) {
            ad.BadgeURL = StaticDataManager.LocalifyUrl(ad.BadgeURL);
            ad.BadgeLockedURL = StaticDataManager.LocalifyUrl(ad.BadgeLockedURL);
        }

        // modify game ids in subsets
        foreach (SetData set in patch.Sets) {
            set.GameId = StaticDataManager.RAIntegrationAssertionWorkaround(overrideId);
        }

        // remove "unsupported emulator"
        gameData.DeleteAchievementById(StaticDataManager.UNSUPPORTED_EMULATOR_ACHIEVEMENT_ID);

        Log.RCheevos.LogInformation("Finished getting data from \"{u}\"", Url);

        string fileBase = Program.Config.Get("LAHEE", "DataDirectory") + "\\" + overrideId + "-" + new string(gameData.Title.Where(ch => !Program.INVALID_FILE_NAME_CHARS.Contains(ch)).ToArray());
        string fileData = fileBase + ".set.json";
        string fileHash = fileBase + ".hash.txt";
        if (!File.Exists(fileData)) {
            Log.RCheevos.LogInformation("Creating file {f}", fileData);
            File.WriteAllText(fileData, JsonConvert.SerializeObject(gameData));
        } else {
            Log.RCheevos.LogWarning("File {f} already exists, not overwriting! Delete to force an update!", fileData);
        }

        if (!File.Exists(fileHash)) {
            Log.RCheevos.LogInformation("Creating file {f}", fileHash);
            File.WriteAllLines(fileHash, hashes);
        } else {
            Log.RCheevos.LogWarning("File {f} already exists, not overwriting! Delete to force an update!", fileHash);
        }

        Log.RCheevos.LogInformation("Finished copying achievement definition data for \"{n}\"", gameData.Title);

        Log.RCheevos.LogInformation("Downloading image files... This may take a while...");
        foreach (KeyValuePair<string, string> image in imageDownloads) {
            CheckAndQueryImage(image.Key, image.Value);
        }

        Log.RCheevos.LogInformation("Finished copying achievement image data for \"{n}\"", gameData.Title);

        StaticDataManager.InitializeAchievements();

        if (copyToUsername != null) {
            UserData user = UserManager.GetUserData(copyToUsername) ?? UserManager.RegisterNewUser(copyToUsername);
            GameData game = StaticDataManager.FindGameDataById(overrideId);
            if (!user.GameData.TryGetValue(fetchId, out UserGameData userGameData)) {
                Log.User.LogInformation("Creating new progression for {user} in {game}", user, game);
                userGameData = user.RegisterGame(game);
            }

            RAStartSessionResponse al = Query<RAStartSessionResponse>(HttpMethod.Get, Url, "dorequest.php?r=startsession&u=" + username + "&t=" + sessionToken + "&h=0&l=11.4&g=" + fetchId + "&m=", null);
            if (al != null) {
                foreach (RAStartSessionResponse.RAStartSessionAchievementData ad in al.Unlocks) {
                    userGameData.UnlockAchievement(ad.ID, false, ad.When);
                }

                foreach (RAStartSessionResponse.RAStartSessionAchievementData ad in al.HardcoreUnlocks) {
                    userGameData.UnlockAchievement(ad.ID, true, ad.When);
                }
            } else {
                Log.RCheevos.LogWarning("Failed to copy achievement data for \"{u}\"", username);
            }

            UserManager.Save();
        }

        Log.Main.LogInformation("Operation completed.");
    }

    private static void CheckAndQueryImage(string filename, string url) {
        string path = Program.Config.Get("LAHEE", "BadgeDirectory");
        string basename = Path.GetFileNameWithoutExtension(filename) + ".png";
        string targetPath = path + "\\" + basename;
        Log.RCheevos.LogTrace("Checking image file: {f} at {f2}", basename, targetPath);
        if (File.Exists(targetPath)) {
            return;
        }

        Log.RCheevos.LogDebug("Downloading image file: {u}", url);
        try {
            HttpClient http = new HttpClient();
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", Program.NAME);

            HttpResponseMessage resp = http.Send(req);
            long? len = resp.Content.Headers.ContentLength;
            if (len == null) {
                throw new IOException("missing content-length");
            }

            byte[] data = new byte[len.Value];
            resp.Content.ReadAsStream().ReadExactly(data);

            File.WriteAllBytes(targetPath, data);
        } catch (Exception ex) {
            Log.RCheevos.LogWarning(ex, "Failed to download image: {i}", url);
        }
    }

    private static TResponse Query<TResponse>(HttpMethod method, string host, string path, object request) where TResponse : class {
        HttpClient http = new HttpClient();
        HttpRequestMessage req = new HttpRequestMessage(method, host + "/" + path);
        req.Headers.Add("User-Agent", Program.NAME);

        Log.RCheevos.LogDebug("HTTP " + req.Method + " request to {u}", req.RequestUri);

        if (request != null) {
            req.Content = new StringContent(request.ToString()!);
            Log.RCheevos.LogTrace("Content: {d}", request);
        }

        try {
            HttpResponseMessage resp = http.Send(req);
            Log.RCheevos.LogDebug("Server returned HTTP {h}", resp.StatusCode);
            string content = new StreamReader(resp.Content.ReadAsStream()).ReadToEnd();
            Log.RCheevos.LogTrace("Content: {d}", content);

            if (resp.StatusCode != HttpStatusCode.OK) {
                throw new Exception("Response not OK: " + resp.StatusCode + ", Content: " + content);
            }

            TResponse r = JsonConvert.DeserializeObject<TResponse>(content);
            if (r is RAAnyResponse ra && !ra.Success) {
                RAErrorResponse error = JsonConvert.DeserializeObject<RAErrorResponse>(content);
                throw new Exception("RA request failed: " + error.Error + "(" + error.Code + ")");
            }

            return r;
        } catch (Exception ex) {
            Log.Network.LogError(ex, "Network error");
            return null;
        }
    }

    public static void FetchComments(uint gameId, int achievementId) {
        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            throw new ProtocolException("Unknown game id: " + gameId);
        }

        string apiWeb = Program.Config.Get("LAHEE", "RAFetch", "WebApiKey");

        if (String.IsNullOrWhiteSpace(Url)) {
            throw new ProtocolException("Invalid RAFetch Url in configuration.");
        }

        if (String.IsNullOrWhiteSpace(apiWeb)) {
            throw new ProtocolException("Invalid RAFetch WebApiKey in configuration. Get it from here: " + Url + "/settings");
        }

        RAApiCommentsResponse resp = Query<RAApiCommentsResponse>(HttpMethod.Get, Url, "API/API_GetComments.php?y=" + apiWeb + "&t=2&i=" + achievementId + "&sort=-submitted", null);
        if (resp != null) {
            bool addedComments = false;
            foreach (UserComment uc in resp.Results) {
                if (uc.ULID.Equals(SERVER_ACCOUNT_USER_ID)) { // skip all "comments" that depict status changes (score changed, icon changed, ...)
                    continue;
                }

                uc.AchievementID = achievementId;
                uc.LaheeUUID = Guid.NewGuid();
                StaticDataManager.AddComment(uc, game, false);
                addedComments = true;
            }

            if (!addedComments) {
                throw new ProtocolException("No comments exist for this achievement on the official RA website.");
            }

            StaticDataManager.SaveCommentFile(game);
        } else {
            throw new ProtocolException("Failed to fetch comments for " + achievementId);
        }
    }

    private static string LogInToRealServer() {
        if (SessionToken != null) {
            return SessionToken;
        }

        string username = Program.Config.Get("LAHEE", "RAFetch", "Username");
        string password = Program.Config.Get("LAHEE", "RAFetch", "Password");

        RALoginResponse login = Query<RALoginResponse>(HttpMethod.Get, Url, "dorequest.php?r=login2&u=" + username + "&p=" + password, null);
        if (login == null) {
            return null;
        }

        Log.RCheevos.LogInformation("Logged into RA server at {u} as {n}", Url, login.DisplayName);

        SessionToken = login.Token;

        return login.Token;
    }

    public static List<CodeNote> FetchCodeNotes(GameData game) {
        ArgumentNullException.ThrowIfNull(game);

        string sessionToken = LogInToRealServer();
        if (sessionToken == null) {
            return null;
        }

        string username = Program.Config.Get("LAHEE", "RAFetch", "Username");

        RACodeNotesResponse notes = Query<RACodeNotesResponse>(HttpMethod.Get, Url, "dorequest.php?r=codenotes2&u=" + username + "&t=" + sessionToken + "&g=" + game.ID, null);
        if (notes == null) {
            return null;
        }

        game.CodeNotes = notes.CodeNotes;

        return notes.CodeNotes;
    }
}