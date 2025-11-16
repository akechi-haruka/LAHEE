using System.Net;
using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LAHEE;

public static class RAOfficialServer {
    private const string SERVER_ACCOUNT_USER_ID = "019Z8BMP7E37YNRVDSP8SV266G";

    public static void FetchData(string gameIdStr, string overrideIdStr, bool includeUnofficial, String copyToUsername = null) {
        string url = Configuration.Get("LAHEE", "RAFetch", "Url");
        string apiWeb = Configuration.Get("LAHEE", "RAFetch", "WebApiKey");
        string username = Configuration.Get("LAHEE", "RAFetch", "Username");
        string password = Configuration.Get("LAHEE", "RAFetch", "Password");

        if (String.IsNullOrWhiteSpace(url)) {
            Log.Main.LogError("Invalid RAFetch Url in configuration.");
            return;
        }

        if (String.IsNullOrWhiteSpace(apiWeb)) {
            Log.Main.LogError("Invalid RAFetch WebApiKey in configuration. Get it from here: {u}", url + "/settings");
            return;
        }

        if (String.IsNullOrWhiteSpace(username)) {
            Log.Main.LogError("Invalid RAFetch username in configuration.");
            return;
        }

        if (String.IsNullOrWhiteSpace(password)) {
            Log.Main.LogError("Invalid RAFetch password in configuration.");
            return;
        }

        if (!Int32.TryParse(gameIdStr, out int fetchId)) {
            Log.Main.LogError("Not a valid game ID: {i}", gameIdStr);
            return;
        }

        int overrideId = fetchId;
        if (!String.IsNullOrWhiteSpace(overrideIdStr)) {
            if (!Int32.TryParse(overrideIdStr, out overrideId)) {
                Log.Main.LogError("Not a valid game ID (override ID): {i}", overrideIdStr);
                return;
            }
        }

        RALoginResponse login = Query<RALoginResponse>(HttpMethod.Get, url, "dorequest.php?r=login2&u=" + username + "&p=" + password, null);
        if (login == null) {
            return;
        }

        Log.RCheevos.LogInformation("Logged into RA server at {u} as {n}", url, login.DisplayName);

        int f = includeUnofficial ? 7 : 3;
        RAPatchResponse patch = Query<RAPatchResponse>(HttpMethod.Get, url, "dorequest.php?r=patch&u=" + username + "&t=" + login.Token + "&f=" + f + "&m=&g=" + fetchId, null);
        if (patch == null) {
            return;
        }

        GameData gameData = patch.PatchData;

        List<string> hashes = new List<string>();
        hashes.AddRange(gameData.ROMHashes);

        RAApiHashesResponse hashResponse = Query<RAApiHashesResponse>(HttpMethod.Get, url, "API/API_GetGameHashes.php?y=" + apiWeb + "&i=" + fetchId, null);
        if (hashResponse == null) {
            return;
        }

        foreach (RAApiHashesResponse.Hash h in hashResponse.Results) {
            if (!hashes.Contains(h.MD5)) {
                hashes.Add(h.MD5);
            }
        }

        Dictionary<String, String> imageDownloads = new Dictionary<string, string>();
        imageDownloads.Add(gameData.ImageIcon, gameData.ImageIconURL);
        foreach (AchievementData ad in gameData.Achievements) {
            imageDownloads[ad.BadgeName] = ad.BadgeURL;
            imageDownloads[Path.GetFileNameWithoutExtension(ad.BadgeName) + "_lock.png"] = ad.BadgeLockedURL;
        }

        RACodeNotesResponse notes = Query<RACodeNotesResponse>(HttpMethod.Get, url, "dorequest.php?r=codenotes2&u=" + username + "&t=" + login.Token + "&g=" + fetchId, null);
        if (notes == null) {
            return;
        }

        gameData.CodeNotes = notes.CodeNotes;

        // Achievement data modifications:

        // apply overridden ID for set merges
        gameData.ID = overrideId;
        // re-route image URLs to local
        gameData.ImageIconURL = StaticDataManager.LocalifyUrl(gameData.ImageIconURL);
        foreach (AchievementData ad in gameData.Achievements) {
            ad.BadgeURL = StaticDataManager.LocalifyUrl(ad.BadgeURL);
            ad.BadgeLockedURL = StaticDataManager.LocalifyUrl(ad.BadgeLockedURL);
        }

        // remove "unsupported emulator"
        gameData.Achievements = gameData.Achievements.Where(a => a.ID != StaticDataManager.UNSUPPORTED_EMULATOR_ACHIEVEMENT_ID).ToArray();

        Log.RCheevos.LogInformation("Finished getting data from \"{u}\"", url);

        String fileBase = Configuration.Get("LAHEE", "DataDirectory") + "\\" + overrideId + "-" + (fetchId != overrideId ? "zz-" : "") + new string(gameData.Title.Where(ch => !Program.INVALID_FILE_NAME_CHARS.Contains(ch)).ToArray());
        String fileData = fileBase + ".json";
        String fileHash = fileBase + ".zhash";
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

            RAStartSessionResponse al = Query<RAStartSessionResponse>(HttpMethod.Get, url, "dorequest.php?r=startsession&u=" + username + "&t=" + login.Token + "&h=0&l=11.4&g=" + fetchId + "&m=", null);
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

    private static void CheckAndQueryImage(String filename, String url) {
        string path = Configuration.Get("LAHEE", "BadgeDirectory");
        String basename = Path.GetFileNameWithoutExtension(filename) + ".png";
        String targetPath = path + "\\" + basename;
        Log.RCheevos.LogTrace("Checking image file: {f} at {f2}", basename, targetPath);
        if (!File.Exists(targetPath)) {
            Log.RCheevos.LogDebug("Downloading image file: " + url);
            try {
                HttpClient http = new HttpClient();
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("User-Agent", Program.NAME);

                HttpResponseMessage resp = http.Send(req);
                byte[] data = new byte[(int)resp.Content.Headers.ContentLength];
                resp.Content.ReadAsStream().ReadExactly(data);

                File.WriteAllBytes(targetPath, data);
            } catch (Exception ex) {
                Log.RCheevos.LogWarning(ex, "Failed to download image: {i}", url);
            }
        }
    }

    private static TResponse Query<TResponse>(HttpMethod method, String host, String path, object request) where TResponse : class {
        HttpClient http = new HttpClient();
        HttpRequestMessage req = new HttpRequestMessage(method, host + "/" + path);
        req.Headers.Add("User-Agent", Program.NAME);

        Log.RCheevos.LogDebug("HTTP " + req.Method + " request to {u}", req.RequestUri);

        if (request != null) {
            req.Content = new StringContent(request.ToString());
            Log.RCheevos.LogTrace("Content: {d}", request);
        }

        try {
            HttpResponseMessage resp = http.Send(req);
            Log.RCheevos.LogDebug("Server returned HTTP {h}", resp.StatusCode);
            String content = new StreamReader(resp.Content.ReadAsStream()).ReadToEnd();
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

    public static void FetchComments(int gameId, int achievementId) {
        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            throw new ProtocolException("Unknown game id: " + gameId);
        }

        string url = Configuration.Get("LAHEE", "RAFetch", "Url");
        string apiWeb = Configuration.Get("LAHEE", "RAFetch", "WebApiKey");

        if (String.IsNullOrWhiteSpace(url)) {
            throw new ProtocolException("Invalid RAFetch Url in configuration.");
        }

        if (String.IsNullOrWhiteSpace(apiWeb)) {
            throw new ProtocolException("Invalid RAFetch WebApiKey in configuration. Get it from here: " + url + "/settings");
        }

        RAApiCommentsResponse resp = Query<RAApiCommentsResponse>(HttpMethod.Get, url, "API/API_GetComments.php?y=" + apiWeb + "&t=2&i=" + achievementId + "&sort=-submitted", null);
        if (resp != null) {
            bool addedComments = false;
            foreach (UserComment uc in resp.Results) {
                if (uc.ULID.Equals(SERVER_ACCOUNT_USER_ID)) {
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
}