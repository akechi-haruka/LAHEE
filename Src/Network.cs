using System.Drawing;
using System.Drawing.Imaging;
using HttpMultipartParser;
using LAHEE.Data;
using LAHEE.Util;
using Microsoft.Extensions.Logging;
using RATools.Data;
using WatsonWebserver.Core;
using WatsonWebserver.Lite;
using AchievementType = LAHEE.Data.AchievementType;
using CodeNote = LAHEE.Data.CodeNote;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace LAHEE;

static class Network {
    public const String LOCAL_HOST = "localhost";
    public const String BASE_DIR = "/";
    public static String LocalUrl;

    internal const String RA_ROUTE_HEADER = "X-RA-Route";

    private static WebserverLite server;
    internal static Dictionary<string, Func<HttpContextBase, Task>> RARoutes;

    public static void Initialize() {
        Log.Network.LogDebug("Initializing network...");

        int localPort = Configuration.GetInt("LAHEE", "WebPort");
        LocalUrl = "http://" + LOCAL_HOST + ":" + localPort + BASE_DIR;

        server = new WebserverLite(new WebserverSettings("0.0.0.0", localPort), Routes.DefaultNotFoundRoute);

        server.Events.Logger += WatsonLogger;
        server.Settings.Debug.Responses = Configuration.GetBool("Watson", "DebugResponses");
        server.Settings.Debug.Requests = Configuration.GetBool("Watson", "DebugRequests");
        server.Settings.Debug.Routing = Configuration.GetBool("Watson", "DebugRouting");

        server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, BASE_DIR, Routes.RedirectWeb, Routes.DefaultErrorRoute);
        server.Routes.PreAuthentication.Static.Add(HttpMethod.POST, BASE_DIR + "dorequest.php", Routes.RARequestRoute, Routes.DefaultErrorRoute);
        server.Routes.PreAuthentication.Static.Add(HttpMethod.POST, BASE_DIR + "doupload.php", Routes.RAUploadRoute, Routes.DefaultErrorRoute);

        server.Routes.PreAuthentication.Content = new CacheableContentRouteManager(Configuration.GetInt("Watson", "ResourceCacheSeconds"));
        server.Routes.PreAuthentication.Content.Add(BASE_DIR + "Badge/", true);
        server.Routes.PreAuthentication.Content.Add(BASE_DIR + "UserPic/", true);
        server.Routes.PreAuthentication.Content.Add(BASE_DIR + "Web/", true);

        server.Routes.PostRouting = Routes.PostRouting;

        RARoutes = new Dictionary<string, Func<HttpContextBase, Task>>();
        AddRARoute("laheeinfo", Routes.LaheeInfo);
        AddRARoute("laheeuserinfo", Routes.LaheeUserInfo);
        AddRARoute("laheefetchcomments", Routes.LaheeFetchComments);
        AddRARoute("laheewritecomment", Routes.LaheeWriteComment);
        AddRARoute("laheedeletecomment", Routes.LaheeDeleteComment);
        AddRARoute("laheeflagimportant", Routes.LaheeFlagImportant);
        AddRARoute("laheetriggerfetch", Routes.LaheeTriggerFetch);
        AddRARoute("laheeachievementcode", Routes.LaheeGetAchievementCode);
        AddRARoute("login", Routes.RALogin);
        AddRARoute("login2", Routes.RALogin);
        AddRARoute("gameid", Routes.RAGameId);
        AddRARoute("patch", Routes.RAPatch);
        AddRARoute("startsession", Routes.RAStartSession);
        AddRARoute("awardachievement", Routes.RAAwardAchievement);
        AddRARoute("ping", Routes.RAPing);
        AddRARoute("submitlbentry", Routes.RASubmitLeaderboardEntry);
        AddRARoute("achievementsets", Routes.RAAchievementSets);
        AddRARoute("latestintegration", Routes.RALatestIntegration);
        AddRARoute("codenotes2", Routes.RACodeNotes2);
        AddRARoute("badgeiter", Routes.RABadgeIter);
        AddRARoute("uploadachievement", Routes.RAUploadAchievement);
        AddRARoute("richpresencepatch", Routes.RARichPresencePatch);

        Log.Network.LogInformation("Starting webserver on {H}:{P}", server.Settings.Hostname, server.Settings.Port);
        server.Start();
        Log.Network.LogDebug("Started.");
    }

    private static void WatsonLogger(string obj) {
        Log.Network.LogDebug(obj);
    }

    private static void AddRARoute(string key, Func<HttpContextBase, Task> route) {
        RARoutes.Add(key, route);
        Log.Network.LogDebug("Added route: " + key);
    }

    public static void Stop() {
        Log.Network.LogDebug("Stopping webserver");
        server.Stop();
    }
}

static class Routes {
    internal static async Task DefaultNotFoundRoute(HttpContextBase ctx) {
        Log.Network.LogWarning("Not found: {u}", ctx.Request.Url.RawWithQuery);
        ctx.Response.StatusCode = 404;
        await ctx.Response.Send("kweh.");
    }

    internal static async Task DefaultErrorRoute(HttpContextBase ctx, Exception e) {
        ctx.Response.StatusCode = 500;
        Log.Network.LogError("Failed to handle request to " + ctx.Request.Url.RawWithQuery + " from " + ctx.Request.Source + ": " + e);
        Log.Network.LogInformation("Request content: " + ctx.Request.DataAsString);
        await ctx.Response.Send(e.Message);
    }

    internal static async Task RedirectWeb(HttpContextBase ctx) {
        ctx.Response.Headers.Add("Location", "Web/");
        ctx.Response.StatusCode = 308;
        await ctx.Response.Send();
    }

    internal static Task PostRouting(HttpContextBase ctx) {
        String raRoute = ctx.Response.Headers.Get(Network.RA_ROUTE_HEADER);
        if (raRoute != null) {
            Log.Network.LogInformation("{Method} {Url} ({RAPath}): {ResponseCode} {ResponseLength} {UserAgent}", ctx.Request.Method, ctx.Request.Url.RawWithQuery, raRoute, ctx.Response.StatusCode, ctx.Response.ContentLength, ctx.Request.Useragent);
        } else {
            Log.Network.LogDebug("{Method} {Url}: {ResponseCode} {ResponseLength} {UserAgent}", ctx.Request.Method, ctx.Request.Url.RawWithQuery, ctx.Response.StatusCode, ctx.Response.ContentLength, ctx.Request.Useragent);
        }

        return Task.CompletedTask;
    }

    internal static async Task RARequestRoute(HttpContextBase ctx) {
        string r = ctx.Request.GetParameter("r");
        Log.Network.LogDebug("RA Request: {r}", r);
        ctx.Response.Headers.Add(Network.RA_ROUTE_HEADER, r);
        if (Network.RARoutes.ContainsKey(r)) {
            await Network.RARoutes[r].Invoke(ctx);
        } else {
            Log.Network.LogError("Request route not found: {r}", r);
            await DefaultNotFoundRoute(ctx);
        }
    }

    internal static async Task RAUploadRoute(HttpContextBase ctx) {
        String part = ctx.Request.ContentType.Split('=')[1];
        Log.Network.LogDebug("Upload request. form-data part = {f}", part);
        using (MemoryStream ms = new MemoryStream(ctx.Request.DataAsBytes)) {
            MultipartFormDataParser form = await MultipartFormDataParser.ParseAsync(ms);
            string r = form.GetParameterValue("r");
            if (r == null) {
                r = "uploadbadgeimage";
            }

            Log.Network.LogDebug("RA Upload Request: {r}", r);
            ctx.Response.Headers.Add(Network.RA_ROUTE_HEADER, r);
            if (r == "uploadbadgeimage") {
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 1)) {
                    if (form.Files.Count > 0) {
                        string badgeDirectory = Configuration.Get("LAHEE", "BadgeDirectory");
                        int badgeId = StaticDataManager.AssignNextCustomAchievementId();

                        Image img = Image.FromStream(form.Files[0].Data);
                        Bitmap resized = Utils.ResizeImage(img, 64, 64);
                        resized.Save(Path.Combine(badgeDirectory, badgeId + ".png"), ImageFormat.Png);

                        Bitmap locked = Utils.MakeGrayscale3(resized);
                        locked.Save(Path.Combine(badgeDirectory, badgeId + "_lock.png"), ImageFormat.Png);

                        Log.Data.LogInformation("Saved uploaded badge image to {id}.png", badgeId);
                        await ctx.Response.SendJson(new RAUploadFileResponse() {
                            Success = true,
                            Response = new RAUploadFileResponse.ResponseClass() {
                                BadgeIter = badgeId.ToString()
                            }
                        });
                    } else {
                        await ctx.Response.Send("No file provided.");
                    }
                } else {
                    await ctx.Response.Send("This is only supported on Windows.");
                }
            } else {
                Log.Network.LogError("Request route not found: {r}", r);
                await DefaultNotFoundRoute(ctx);
            }
        }
    }

    internal static async Task RALogin(HttpContextBase ctx) {
        string username = ctx.Request.GetParameter("u");
        //string password = ctx.Request.GetParameter("p");
        string token = ctx.Request.GetParameter("t");

        UserData user = UserManager.GetUserData(username);
        if (user == null) {
            Log.User.LogWarning("No user found with username {n}, creating a new user.", username);
            user = UserManager.RegisterNewUser(username);

            UserManager.Save();
        }

        if (!user.AllowUse) {
            Log.User.LogWarning("Blocking login of {u}", user);
            await ctx.Response.SendJson(new RAErrorResponse("User login for " + user + " is blocked! Check console if there were any errors when loading user data."));
            return;
        }

        if (token != null) {
            UserManager.RegisterSessionToken(user, token);
        } else {
            token = UserManager.RegisterSessionToken(user);
        }

        RALoginResponse response = new RALoginResponse() {
            Success = true,
            User = user.UserName,
            Token = token,
            Score = user.GetScore(true),
            SoftcoreScore = user.GetScore(false),
            AccountType = "Registered",
            DisplayName = user.UserName
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RAGameId(HttpContextBase ctx) {
        string hash = ctx.Request.GetParameter("m");

        GameData game = StaticDataManager.FindGameDataByHash(hash);
        if (game == null) {
            Log.User.LogWarning("ROM Hash {hash} not registered!", hash);
        }

        RAGameIDResponse response = new RAGameIDResponse() {
            Success = true,
            GameID = game?.ID ?? 0
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RAPatch(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        //String token = ctx.Request.GetParameter("t");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            Log.User.LogWarning("Game ID {id} not registered!", gameId);
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        RAPatchResponse response = new RAPatchResponse() {
            Success = true,
            PatchData = game
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RAStartSession(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        String token = ctx.Request.GetParameter("t");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));
        int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
        //String gamehash = ctx.Request.GetParameter("m");
        //String libraryVersion = ctx.Request.GetParameter("l"); // 11.4

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            Log.User.LogWarning("Game ID {id} not registered!", gameId);
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        if (!user.GameData.TryGetValue(gameId, out UserGameData userGameData)) {
            Log.User.LogInformation("Creating new progression for {user} in {game}", user, game);
            userGameData = user.RegisterGame(game);
            UserManager.Save();
        }

        userGameData.PlayTimeLastPing = DateTime.Now;
        userGameData.LastPlay = DateTime.Now;

        List<RAStartSessionResponse.RAStartSessionAchievementData> softcore = new List<RAStartSessionResponse.RAStartSessionAchievementData>();
        List<RAStartSessionResponse.RAStartSessionAchievementData> hardcore = new List<RAStartSessionResponse.RAStartSessionAchievementData>();

        foreach (UserAchievementData userAchievement in userGameData.Achievements.Values) {
            if (userAchievement.Status == UserAchievementData.StatusFlag.SoftcoreUnlock) {
                softcore.Add(new RAStartSessionResponse.RAStartSessionAchievementData(userAchievement, false));
            } else if (userAchievement.Status == UserAchievementData.StatusFlag.HardcoreUnlock) {
                hardcore.Add(new RAStartSessionResponse.RAStartSessionAchievementData(userAchievement, true));
            }
        }

        Log.User.LogDebug("Sending {c} softcore unlocks", softcore.Count);
        Log.User.LogDebug("Sending {c} hardcore unlocks", hardcore.Count);

        Log.User.LogInformation("{user} started a session of \"{game}\" in {mode} mode", user, game, hardcoreFlag == 1 ? "Hardcore" : "Softcore");

        RAStartSessionResponse response = new RAStartSessionResponse() {
            Success = true,
            ServerNow = Utils.CurrentUnixSeconds,
            Unlocks = softcore.ToArray(),
            HardcoreUnlocks = hardcore.ToArray()
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RAAwardAchievement(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        String token = ctx.Request.GetParameter("t");
        int achievementid = Int32.Parse(ctx.Request.GetParameter("a"));
        int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
        String gamehash = ctx.Request.GetParameter("m");
        // int secondsSinceUnlock = ctx.Request.GetParameter("o");
        //String verification = ctx.Request.GetParameter("v");

        GameData game = StaticDataManager.FindGameDataByHash(gamehash);
        if (game == null) {
            Log.User.LogWarning("ROM Hash {hash} not registered!", gamehash);
            await ctx.Response.SendJson(new RAErrorResponse("ROM hash is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        AchievementData ach = game.GetAchievementById(achievementid);
        if (ach == null) {
            Log.User.LogWarning("Achievement with ID {id} not found in game \"{game}\"!", achievementid, game);
            await ctx.Response.SendJson(new RAErrorResponse("Achievement ID not found!"));
            return;
        }

        UserGameData userGameData = user.GameData[game.ID];

        if (userGameData.Achievements.TryGetValue(achievementid, out UserAchievementData userAchievementData)) {
            if (userAchievementData.Status == UserAchievementData.StatusFlag.HardcoreUnlock || (userAchievementData.Status == UserAchievementData.StatusFlag.SoftcoreUnlock && hardcoreFlag == 0)) {
                Log.User.LogWarning("{user} sent unlock for achievement \"{ach}\" in \"{game}\", but already has it! (status={s},hardcore submission={hc})", user, ach, game, userAchievementData.Status, hardcoreFlag);
                await ctx.Response.SendJson(new RAErrorResponse("User already has this achievement"));
                return;
            }
        }

        userAchievementData = userGameData.UnlockAchievement(achievementid, hardcoreFlag == 1);

        Log.User.LogInformation("{user} has unlocked \"{ach}\" in \"{game}\" in {mode} mode!", user, ach, game, hardcoreFlag == 1 ? "Hardcore" : "Softcore");
        UserManager.Save();

        LiveTicker.BroadcastUnlock(game.ID, userAchievementData);
        LiveTicker.BroadcastPing(LiveTicker.LiveTickerEventPing.PingType.AchievementUnlock);
        CaptureManager.StartCapture(game, user, ach);

        int totalAchievementCount = game.GetAchievementCount();
        int userAchieved = userGameData.Achievements.Count(a => a.Value.Status == (hardcoreFlag == 1 ? UserAchievementData.StatusFlag.HardcoreUnlock : UserAchievementData.StatusFlag.SoftcoreUnlock));

        RAUnlockResponse response = new RAUnlockResponse() {
            Success = true,
            AchievementID = achievementid,
            Score = user.GetScore(true),
            SoftcoreScore = user.GetScore(false),
            AchievementsRemaining = totalAchievementCount - userAchieved
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RAPing(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        String token = ctx.Request.GetParameter("t");
        //uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));
        String gameHash = ctx.Request.GetParameter("x");
        String statusMessage = ctx.Request.GetParameter("m");
        //int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
        // int secondsSinceUnlock = ctx.Request.GetParameter("o");

        GameData game = StaticDataManager.FindGameDataByHash(gameHash);
        if (game == null) {
            Log.User.LogWarning("ROM Hash {hash} not registered!", gameHash);
            await ctx.Response.SendJson(new RAErrorResponse("ROM hash is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        user.CurrentGameId = game.ID;

        UserGameData userGameData = user.GameData[game.ID];

        userGameData.PlayTimeApprox += DateTime.Now - userGameData.PlayTimeLastPing.GetValueOrDefault(DateTime.Now);
        userGameData.PlayTimeLastPing = DateTime.Now;
        userGameData.LastPresence = statusMessage;
        if (userGameData.PresenceHistory == null) { // 1.0 backcompat
            userGameData.PresenceHistory = new List<PresenceHistory>();
        }

        userGameData.PresenceHistory.Add(new PresenceHistory(DateTime.Now, statusMessage));

        int limit = Configuration.GetInt("LAHEE", "PresenceHistoryLimit");
        while (limit > -1 && userGameData.PresenceHistory.Count > limit) {
            userGameData.PresenceHistory.RemoveAt(0);
        }

        UserManager.Save();

        LiveTicker.BroadcastPing(LiveTicker.LiveTickerEventPing.PingType.Time);

        RAErrorResponse response = new RAErrorResponse(null) {
            Success = true
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RASubmitLeaderboardEntry(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        String token = ctx.Request.GetParameter("t");
        int leaderboardId = Int32.Parse(ctx.Request.GetParameter("i"));
        int score = Int32.Parse(ctx.Request.GetParameter("s"));
        String gamehash = ctx.Request.GetParameter("m");
        // int secondsSinceUnlock = ctx.Request.GetParameter("o");
        // uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));
        //String verification = ctx.Request.GetParameter("v");

        GameData game = StaticDataManager.FindGameDataByHash(gamehash);
        if (game == null) {
            Log.User.LogWarning("ROM Hash {hash} not registered!", gamehash);
            await ctx.Response.SendJson(new RAErrorResponse("ROM hash is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        UserGameData userGameData = user.GameData[game.ID];

        LeaderboardData leaderboardData = game.GetLeaderboardById(leaderboardId);
        if (leaderboardData == null) {
            Log.User.LogWarning("Leaderboard with ID {id} not found in game \"{game}\"!", leaderboardId, game);
            await ctx.Response.SendJson(new RAErrorResponse("Leaderboard ID not found!"));
            return;
        }

        if (userGameData.LeaderboardEntries == null) { // backcompat
            userGameData.LeaderboardEntries = new Dictionary<int, List<UserLeaderboardData>>();
        }

        if (!userGameData.LeaderboardEntries.ContainsKey(leaderboardId)) {
            userGameData.LeaderboardEntries.Add(leaderboardId, new List<UserLeaderboardData>());
        }

        userGameData.LeaderboardEntries[leaderboardId].Add(new UserLeaderboardData() {
            LeaderboardID = leaderboardId,
            Score = score,
            RecordDate = Utils.CurrentUnixSeconds,
            PlayTime = userGameData.PlayTimeApprox + (DateTime.Now - userGameData.PlayTimeLastPing.GetValueOrDefault(DateTime.Now))
        });

        Log.User.LogInformation("{user} recorded a score of {score} on the leaderboard \"{lb}\" in \"{game}\"", user, score, leaderboardData, game);
        UserManager.Save();

        LiveTicker.BroadcastPing(LiveTicker.LiveTickerEventPing.PingType.LeaderboardRecorded);

        RALeaderboardResponse response = new RALeaderboardResponse() {
            Success = true,
            Score = score,
            BestScore = userGameData.LeaderboardEntries[leaderboardId].Select(r => r.Score).Max(),
            RankInfo = new RALeaderboardResponse.RankObject() {
                NumEntries = 1, // out of scope
                Rank = 1
            },
            TopEntries = new RALeaderboardResponse.TopObject[0], // out of scope
            Response = new RALeaderboardResponseV2() {
                Success = true,
                LBData = leaderboardData,
                Score = score,
                ScoreFormatted = score.ToString(), // TODO?
                BestScore = userGameData.LeaderboardEntries[leaderboardId].Select(r => r.Score).Max(),
                RankInfo = new RALeaderboardResponse.RankObject() {
                    NumEntries = 1, // out of scope
                    Rank = 1
                },
                TopEntries = new RALeaderboardResponse.TopObject[0], // out of scope
                TopEntriesFriends = new RALeaderboardResponse.TopObject[0], // out of scope
            }
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeInfo(HttpContextBase ctx) {
        LaheeResponse response = new LaheeResponse() {
            version = Program.NAME,
            games = StaticDataManager.GetAllGameData().ToArray(),
            users = UserManager.GetAllUserData(),
            comments = StaticDataManager.GetAllUserComments()
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeUserInfo(HttpContextBase ctx) {
        String user = ctx.Request.GetParameter("user");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));

        UserData userData = UserManager.GetUserData(user);
        if (userData == null) {
            await ctx.Response.SendJson(new RAErrorResponse("User does not exist!"));
            return;
        }

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        userData.GameData.TryGetValue(gameId, out UserGameData userGameData);

        LaheeUserResponse response = new LaheeUserResponse() {
            current_game_id = userData.CurrentGameId,
            game_status = userGameData?.LastPresence,
            last_ping = userGameData?.PlayTimeLastPing,
            last_play = userGameData?.LastPlay,
            play_time = userGameData?.PlayTimeApprox,
            achievements = userGameData?.Achievements ?? []
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeFetchComments(HttpContextBase ctx) {
        String user = ctx.Request.GetParameter("user");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));
        int achievementId = Int32.Parse(ctx.Request.GetParameter("aid"));

        UserData userData = UserManager.GetUserData(user);
        if (userData == null) {
            await ctx.Response.SendJson(new RAErrorResponse("User does not exist!"));
            return;
        }

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        AchievementData ach = game.GetAchievementById(achievementId);
        if (ach == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Achievement ID not found!"));
            return;
        }

        try {
            RAOfficialServer.FetchComments(gameId, ach.ID);
        } catch (ProtocolException e) {
            Log.RCheevos.LogError(e.Message);
            await ctx.Response.SendJson(new RAErrorResponse(e.Message));
        } catch (Exception e) {
            Log.RCheevos.LogError(e, "Exception while downloading comments for {g}/{a}", game, ach);
            await ctx.Response.SendJson(new RAErrorResponse("Downloading comments from official RA server failed"));
            return;
        }

        LaheeFetchCommentsResponse response = new LaheeFetchCommentsResponse() {
            Success = true,
            Comments = StaticDataManager.GetAllUserComments()
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeWriteComment(HttpContextBase ctx) {
        String user = ctx.Request.GetParameter("user");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));
        int achievementId = Int32.Parse(ctx.Request.GetParameter("aid"));
        String comment = ctx.Request.GetParameter("comment");

        UserData userData = UserManager.GetUserData(user);
        if (userData == null) {
            await ctx.Response.SendJson(new RAErrorResponse("User does not exist!"));
            return;
        }

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        AchievementData ach = game.GetAchievementById(achievementId);
        if (ach == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Achievement ID not found!"));
            return;
        }

        StaticDataManager.AddComment(userData, game, ach, comment);

        LaheeFetchCommentsResponse response = new LaheeFetchCommentsResponse() {
            Success = true,
            Comments = StaticDataManager.GetAllUserComments()
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeDeleteComment(HttpContextBase ctx) {
        String uuid = ctx.Request.GetParameter("uuid");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        if (!StaticDataManager.DeleteComment(game, uuid)) {
            await ctx.Response.SendJson(new RAErrorResponse("Comment ID not found"));
            return;
        }

        LaheeFetchCommentsResponse response = new LaheeFetchCommentsResponse() {
            Success = true,
            Comments = StaticDataManager.GetAllUserComments()
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeFlagImportant(HttpContextBase ctx) {
        String user = ctx.Request.GetParameter("user");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));
        int achievementId = Int32.Parse(ctx.Request.GetParameter("aid"));

        UserData userData = UserManager.GetUserData(user);
        if (userData == null) {
            await ctx.Response.SendJson(new RAErrorResponse("User does not exist!"));
            return;
        }

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        AchievementData ach = game.GetAchievementById(achievementId);
        if (ach == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Achievement ID not found!"));
            return;
        }

        UserGameData userGameData = userData.GameData[game.ID];
        if (userGameData.FlaggedAchievements == null) {
            userGameData.FlaggedAchievements = new List<int>();
        }

        bool isFlagged = userGameData.FlaggedAchievements.Contains(ach.ID);
        if (isFlagged) {
            userGameData.FlaggedAchievements.Remove(ach.ID);
        } else {
            userGameData.FlaggedAchievements.Add(ach.ID);
        }

        UserManager.Save();

        LaheeFlagImportantResponse response = new LaheeFlagImportantResponse() {
            Success = true,
            Flagged = userGameData.FlaggedAchievements
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeTriggerFetch(HttpContextBase ctx) {
        String gameid = ctx.Request.GetParameter("gameid");
        String @override = ctx.Request.GetParameter("override");
        bool unofficial = ctx.Request.GetParameter("unofficial") == "true";

        RAOfficialServer.FetchData(gameid, @override, unofficial);

        RAAnyResponse response = new RAAnyResponse() {
            Success = true
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task RAAchievementSets(HttpContextBase ctx) {
        //String username = ctx.Request.GetParameter("u");
        String token = ctx.Request.GetParameter("t");
        String gameHash = ctx.Request.GetParameter("m");
        uint gameId = 0;
        if (ctx.Request.GetParameter("g") != null) {
            gameId = UInt32.Parse(ctx.Request.GetParameter("g"));
        }

        GameData game = gameId > 0 ? StaticDataManager.FindGameDataById(gameId) : StaticDataManager.FindGameDataByHash(gameHash);
        if (game == null) {
            Log.User.LogWarning("Game {hash} not registered!", gameId > 0 ? gameId : gameHash);
            await ctx.Response.SendJson(new RAErrorResponse("ROM hash is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        List<SetData> sets;
        if (Configuration.GetBool("LAHEE", "LoadAsSingleSet")) {
            sets = new List<SetData>() {
                new SetData() {
                    Achievements = game.GetAllAchievements().ToList(),
                    Leaderboards = game.GetAllLeaderboards().ToList(),
                    AchievementSetId = 1,
                    GameId = StaticDataManager.RAIntegrationAssertionWorkaround(game.ID),
                    Title = game.Title,
                    ImageIconUrl = game.ImageIconURL,
                    Type = SetType.core
                }
            };
        } else {
            sets = game.AchievementSets;
        }

        RAPatchResponseV2 response = new RAPatchResponseV2() {
            Success = true,
            GameId = game.ID,
            Title = game.Title,
            ImageIconUrl = game.ImageIconURL,
            RichPresenceGameId = game.ID,
            RichPresencePatch = game.RichPresencePatch,
            ConsoleId = game.ConsoleID,
            Sets = sets
        };

        await ctx.Response.SendJson(response);
    }

    internal static async Task RALatestIntegration(HttpContextBase ctx) {
        RALatestIntegrationResponse response = new RALatestIntegrationResponse() {
            Success = false,
            LatestVersion = "0.0",
            MinimumVersion = "0.0",
            LatestVersionUrl = "",
            LatestVersionUrlX64 = ""
        }; // TODO: ?
        await ctx.Response.SendJson(response);
    }

    internal static async Task RACodeNotes2(HttpContextBase ctx) {
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            Log.User.LogWarning("Game ID {id} not registered!", gameId);
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        if (Configuration.GetBool("LAHEE", "RAFetch", "AutoUpdateCodeNotes")) {
            try {
                if (game.CodeNotes == null || game.CodeNotes.Count == 0) {
                    if (RAOfficialServer.CanFetch) {
                        List<CodeNote> codeNotes = RAOfficialServer.FetchCodeNotes(game);
                        if (codeNotes == null) {
                            throw new ProtocolException("Internal error while downloading code notes");
                        }
                    } else {
                        Log.RCheevos.LogWarning("Not downloading code notes, RAFetch is not configured correctly");
                    }
                }
            } catch (ProtocolException e) {
                Log.RCheevos.LogError(e.Message);
                await ctx.Response.SendJson(new RAErrorResponse(e.Message));
            } catch (Exception e) {
                Log.RCheevos.LogError(e, "Exception while downloading code notes for {g}", game);
                await ctx.Response.SendJson(new RAErrorResponse("Downloading code notes from official RA server failed"));
                return;
            }
        } else {
            Log.RCheevos.LogInformation("Auto-updating code notes is disabled");
        }

        RACodeNotesResponse response = new RACodeNotesResponse() {
            Success = true,
            CodeNotes = game.CodeNotes
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task RARichPresencePatch(HttpContextBase ctx) {
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            Log.User.LogWarning("Game ID {id} not registered!", gameId);
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        RARichPresencePatchResponse response = new RARichPresencePatchResponse() {
            Success = true,
            RichPresencePatch = game.RichPresencePatch
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task RABadgeIter(HttpContextBase ctx) {
        RABadgeIterResponse response = new RABadgeIterResponse() {
            Success = true,
            FirstBadge = 1,
            NextBadge = 1
        }; // TODO: ?????
        await ctx.Response.SendJson(response);
    }

    internal static async Task RAUploadAchievement(HttpContextBase ctx) {
        String token = ctx.Request.GetParameter("t");
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("g"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            Log.User.LogWarning("Game ID {id} not registered!", gameId);
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        UserData user = UserManager.GetUserDataFromToken(token);
        if (user == null) {
            Log.User.LogWarning("Session token not found: {token}!", token);
            await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
            return;
        }

        Log.Data.LogInformation("Updating achievement: {aid}", ctx.Request.GetParameter("a"));

        int achievementId = Int32.Parse(ctx.Request.GetParameter("a") ?? "-1");
        String title = ctx.Request.GetParameter("n");
        String text = ctx.Request.GetParameter("d");
        int points = Int32.Parse(ctx.Request.GetParameter("z"));
        String type = ctx.Request.GetParameter("x");
        String code = ctx.Request.GetParameter("m");
        int flag = Int32.Parse(ctx.Request.GetParameter("f"));
        String icon = ctx.Request.GetParameter("b");

        if (achievementId <= 0) {
            achievementId = StaticDataManager.AssignNextCustomAchievementId();
        }

        string badgeDirectory = Configuration.Get("LAHEE", "BadgeDirectory");

        AchievementData existing = game.GetAchievementById(achievementId);

        AchievementData ach = new AchievementData {
            ID = achievementId,
            MemAddr = code,
            Title = title,
            Description = text,
            Points = points,
            Author = existing?.Author ?? user.UserName,
            Modified = Utils.CurrentUnixSeconds,
            Created = existing?.Created ?? Utils.CurrentUnixSeconds,
            BadgeName = icon,
            Flags = (AchievementFlags)flag,
            Rarity = existing?.Rarity ?? 0,
            RarityHardcore = existing?.RarityHardcore ?? 0,
            BadgeURL = "/" + badgeDirectory + "/" + icon + ".png",
            BadgeLockedURL = "/" + badgeDirectory + "/" + icon + "_lock.png"
        };

        ach.Type = Enum.TryParse(type, out AchievementType t) ? t : null;

        game.DeleteAchievementById(achievementId);
        game.GetCoreAchievementSet()?.Achievements?.Add(ach);
        StaticDataManager.SaveSingleAchievement(game, ach);

        Log.Network.LogTrace("Response id: {id}", achievementId);
        RAUploadAchievementResponse response = new RAUploadAchievementResponse() {
            Success = true,
            AchievementID = achievementId,
            Error = ""
        };
        await ctx.Response.SendJson(response);
    }

    internal static async Task LaheeGetAchievementCode(HttpContextBase ctx) {
        uint gameId = UInt32.Parse(ctx.Request.GetParameter("gameid"));
        uint achievementId = UInt32.Parse(ctx.Request.GetParameter("aid"));

        GameData game = StaticDataManager.FindGameDataById(gameId);
        if (game == null) {
            await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
            return;
        }

        AchievementData ach = game.GetAchievementById(achievementId);
        if (ach == null) {
            Log.User.LogWarning("Achievement with ID {id} not found in game \"{game}\"!", achievementId, game);
            await ctx.Response.SendJson(new RAErrorResponse("Achievement ID not found!"));
            return;
        }

        if (Configuration.GetBool("LAHEE", "RAFetch", "AutoUpdateCodeNotes")) {
            try {
                if (game.CodeNotes == null || game.CodeNotes.Count == 0) {
                    if (RAOfficialServer.CanFetch) {
                        List<CodeNote> codeNotes = RAOfficialServer.FetchCodeNotes(game);
                        if (codeNotes == null) {
                            throw new ProtocolException("Internal error while downloading code notes");
                        }
                    } else {
                        Log.RCheevos.LogWarning("Not downloading code notes, RAFetch is not configured correctly");
                    }
                }
            } catch (ProtocolException e) {
                Log.RCheevos.LogError(e.Message);
                await ctx.Response.SendJson(new RAErrorResponse(e.Message));
            } catch (Exception e) {
                Log.RCheevos.LogError(e, "Exception while downloading code notes for {g}", game);
                await ctx.Response.SendJson(new RAErrorResponse("Downloading code notes from official RA server failed"));
                return;
            }
        } else {
            Log.RCheevos.LogInformation("Auto-updating code notes is disabled");
        }

        Trigger trigger;
        try {
            trigger = Trigger.Deserialize(ach.MemAddr);
            if (trigger == null) {
                throw new NullReferenceException("Trigger deserialization failed");
            }
        } catch (Exception e) {
            Log.RCheevos.LogError(e.Message);
            await ctx.Response.SendJson(new RAErrorResponse(e.Message));
            return;
        }

        LaheeAchievementCodeResponse response = new LaheeAchievementCodeResponse() {
            Success = true,
            CodeNotes = game.CodeNotes,
            TriggerGroups = trigger.Groups.ToArray()
        };
        await ctx.Response.SendJson(response);
    }
}