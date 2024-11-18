using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WatsonWebserver;
using WatsonWebserver.Core;
using HttpMethod = WatsonWebserver.Core.HttpMethod;

namespace LAHEE {
    internal class Network {

        public const String LOCAL_HOST = "localhost";
        public const int LOCAL_PORT = 8000;
        public const String BASE_DIR = "/";
        public const String LOCAL_URL = "http://" + LOCAL_HOST + ":8000" + BASE_DIR;

        internal const String RA_ROUTE_HEADER = "X-RA-Route";

        private static Webserver server;
        internal static Dictionary<string, Func<HttpContextBase, Task>> raRoutes;

        public static void Initialize() {

            Log.Network.LogDebug("Initalizing network...");

            server = new Webserver(new WebserverSettings(LOCAL_HOST, LOCAL_PORT), Routes.DefaultNotFoundRoute);

            server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, BASE_DIR, Routes.RedirectWeb, Routes.DefaultErrorRoute);
            server.Routes.PreAuthentication.Static.Add(HttpMethod.POST, BASE_DIR + "dorequest.php", Routes.RARequestRoute, Routes.DefaultErrorRoute);

            server.Routes.PreAuthentication.Content.Add(BASE_DIR + "Badge/", true);
            server.Routes.PreAuthentication.Content.Add(BASE_DIR + "UserPic/", true);
            server.Routes.PreAuthentication.Content.Add(BASE_DIR + "Web/", true);

            server.Routes.PostRouting = Routes.PostRouting;

            raRoutes = new Dictionary<string, Func<HttpContextBase, Task>>();
            AddRARoute("laheeinfo", Routes.LaheeInfo);
            AddRARoute("laheeuserinfo", Routes.LaheeUserInfo);
            AddRARoute("login", Routes.RALogin);
            AddRARoute("login2", Routes.RALogin);
            AddRARoute("gameid", Routes.RAGameId);
            AddRARoute("patch", Routes.RAPatch);
            AddRARoute("startsession", Routes.RAStartSession);
            AddRARoute("awardachievement", Routes.RAAwardAchievement);
            AddRARoute("ping", Routes.RAPing);
            AddRARoute("submitlbentry", Routes.RASubmitLeaderboardEntry);

            Log.Network.LogInformation("Starting webserver on {H}:{P}", server.Settings.Hostname, server.Settings.Port);
            server.Start();
            Log.Network.LogDebug("Started.");
        }

        private static void AddRARoute(string key, Func<HttpContextBase, Task> route) {
            raRoutes.Add(key, route);
            Log.Network.LogDebug("Added route: " + key);
        }

        public static void Stop() {
            Log.Network.LogDebug("Stopping webserver");
            server.Stop();
        }
    }

    internal class Routes {
        internal static async Task DefaultNotFoundRoute(HttpContextBase ctx) {
            ctx.Response.StatusCode = 404;
            await ctx.Response.Send("kweh.");
        }

        internal static async Task DefaultErrorRoute(HttpContextBase ctx, Exception e) {
            ctx.Response.StatusCode = 500;
            Log.Network.LogError("Failed to handle request to " + ctx.Request.Url.Full + " from " + ctx.Request.Source + ": " + e);
            await ctx.Response.Send(e.Message);
        }

        internal static async Task RedirectWeb(HttpContextBase ctx) {
            ctx.Response.Headers.Add("Location", "Web/");
            ctx.Response.StatusCode = 308;
            await ctx.Response.Send();
        }

        internal static async Task PostRouting(HttpContextBase ctx) {
            Log.Network.LogInformation("{Method} {Url} ({RAPath}): {ResponseCode} {ResponseLength} {UserAgent}", ctx.Request.Method, ctx.Request.Url.Full, ctx.Response.Headers.Get(Network.RA_ROUTE_HEADER) ?? "N/A", ctx.Response.StatusCode, ctx.Response.ContentLength, ctx.Request.Useragent);
        }

        internal static async Task RARequestRoute(HttpContextBase ctx) {
            string r = ctx.Request.GetParameter("r");
            Log.Network.LogDebug("RA Request: {r}", r);
            ctx.Response.Headers.Add(Network.RA_ROUTE_HEADER, r);
            if (Network.raRoutes.ContainsKey(r)) {
                await Network.raRoutes[r].Invoke(ctx);
            } else {
                Log.Network.LogError("Request route not found: {r}", r);
                await DefaultNotFoundRoute(ctx);
            }
        }

        internal static async Task RALogin(HttpContextBase ctx) {
            string username = ctx.Request.GetParameter("u");
            string password = ctx.Request.GetParameter("p");
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
                await ctx.Response.SendJson(new RAErrorResponse("ROM hash is not registered!"));
                return;
            }

            RAGameIDResponse response = new RAGameIDResponse() {
                Success = true,
                GameID = game.ID
            };

            await ctx.Response.SendJson(response);
        }

        internal static async Task RAPatch(HttpContextBase ctx) {
            String username = ctx.Request.GetParameter("u");
            String token = ctx.Request.GetParameter("t");
            int gameid = Int32.Parse(ctx.Request.GetParameter("g"));

            GameData game = StaticDataManager.FindGameDataById(gameid);
            if (game == null) {
                Log.User.LogWarning("Game ID {id} not registered!", gameid);
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
            String username = ctx.Request.GetParameter("u");
            String token = ctx.Request.GetParameter("t");
            int gameid = Int32.Parse(ctx.Request.GetParameter("g"));
            int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
            String gamehash = ctx.Request.GetParameter("m");
            String libraryVersion = ctx.Request.GetParameter("l"); // 11.4

            GameData game = StaticDataManager.FindGameDataById(gameid);
            if (game == null) {
                Log.User.LogWarning("Game ID {id} not registered!", gameid);
                await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
                return;
            }

            UserData user = UserManager.GetUserDataFromToken(token);
            if (user == null) {
                Log.User.LogWarning("Session token not found: {token}!", token);
                await ctx.Response.SendJson(new RAErrorResponse("Session token not found!"));
                return;
            }

            if (!user.GameData.TryGetValue(gameid, out UserGameData userGameData)) {
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
                    softcore.Add(new RAStartSessionResponse.RAStartSessionAchievementData(userAchievement, true));
                } else if (userAchievement.Status == UserAchievementData.StatusFlag.SoftcoreUnlock) {
                    hardcore.Add(new RAStartSessionResponse.RAStartSessionAchievementData(userAchievement, false));
                }
            }

            Log.User.LogInformation("{user} started a session of \"{game}\" in {mode} mode", user, game, hardcoreFlag == 1 ? "Hardcore" : "Softcore");

            RAStartSessionResponse response = new RAStartSessionResponse() {
                Success = true,
                ServerNow = Util.CurrentUnixSeconds,
                Unlocks = softcore.ToArray(),
                HardcoreUnlocks = hardcore.ToArray()
            };

            await ctx.Response.SendJson(response);
        }

        internal static async Task RAAwardAchievement(HttpContextBase ctx) {
            String username = ctx.Request.GetParameter("u");
            String token = ctx.Request.GetParameter("t");
            int achievementid = Int32.Parse(ctx.Request.GetParameter("a"));
            int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
            String gamehash = ctx.Request.GetParameter("m");
            // int secondsSinceUnlock = ctx.Request.GetParameter("o");
            String verification = ctx.Request.GetParameter("v");

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
                if (userAchievementData.Status == UserAchievementData.StatusFlag.HardcoreUnlock || (userAchievementData.Status == UserAchievementData.StatusFlag.SoftcoreUnlock && hardcoreFlag == 0))
                    Log.User.LogWarning("{user} sent unlock for achievement \"{ach}\" in \"{game}\", but already has it!", user, ach, game);
                await ctx.Response.SendJson(new RAErrorResponse("User already has this achievement"));
                return;
            }

            if (userAchievementData == null) {
                userAchievementData = new UserAchievementData() {
                    AchievementID = achievementid
                };
                userGameData.Achievements[achievementid] = userAchievementData;
            }

            if (hardcoreFlag == 1) {
                userAchievementData.Status = UserAchievementData.StatusFlag.HardcoreUnlock;
                userAchievementData.AchieveDate = Util.CurrentUnixSeconds;
                userAchievementData.AchievePlaytime = userGameData.PlayTimeApprox + (DateTime.Now - userGameData.PlayTimeLastPing);
            } else if (userAchievementData.Status == UserAchievementData.StatusFlag.Locked) {
                userAchievementData.Status = UserAchievementData.StatusFlag.SoftcoreUnlock;
                userAchievementData.AchieveDateSoftcore = Util.CurrentUnixSeconds;
                userAchievementData.AchievePlaytimeSoftcore = userGameData.PlayTimeApprox + (DateTime.Now - userGameData.PlayTimeLastPing);
            }

            Log.User.LogInformation("{user} has unlocked \"{ach}\" in \"{game}\" in {mode} mode!", user, ach, game, hardcoreFlag == 1 ? "Hardcore" : "Softcore");
            UserManager.Save();

            LiveTicker.BroadcastPing();

            int totalAchievementCount = game.Achievements.Length;
            int userAchieved = userGameData.Achievements.Where(a => a.Value.Status == (hardcoreFlag == 1 ? UserAchievementData.StatusFlag.HardcoreUnlock : UserAchievementData.StatusFlag.SoftcoreUnlock)).Count();

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
            String username = ctx.Request.GetParameter("u");
            String token = ctx.Request.GetParameter("t");
            int gameid = Int32.Parse(ctx.Request.GetParameter("g"));
            String gamehash = ctx.Request.GetParameter("x");
            String statusMessage = ctx.Request.GetParameter("m");
            int hardcoreFlag = Int32.Parse(ctx.Request.GetParameter("h")); // 1/0
            // int secondsSinceUnlock = ctx.Request.GetParameter("o");

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

            user.CurrentGameId = game.ID;

            UserGameData userGameData = user.GameData[game.ID];

            userGameData.PlayTimeApprox += (DateTime.Now - userGameData.PlayTimeLastPing);
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

            LiveTicker.BroadcastPing();

            RAErrorResponse response = new RAErrorResponse(null) {
                Success = true
            };

            await ctx.Response.SendJson(response);
        }

        internal static async Task RASubmitLeaderboardEntry(HttpContextBase ctx) {
            String username = ctx.Request.GetParameter("u");
            String token = ctx.Request.GetParameter("t");
            int leaderboardId = Int32.Parse(ctx.Request.GetParameter("i"));
            int score = Int32.Parse(ctx.Request.GetParameter("s"));
            String gamehash = ctx.Request.GetParameter("m");
            // int secondsSinceUnlock = ctx.Request.GetParameter("o");
            int gameid = Int32.Parse(ctx.Request.GetParameter("g"));
            String verification = ctx.Request.GetParameter("v");

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

            userGameData.LeaderboardEntries.Add(leaderboardId, new UserLeaderboardData() {
                LeaderboardID = leaderboardId,
                Score = score,
                RecordDate = Util.CurrentUnixSeconds
            });

            Log.User.LogInformation("{user} recorded a score of {score} on the leaderboard \"{lb}\" in \"{game}\"", user, score, leaderboardData, game);
            UserManager.Save();

            LiveTicker.BroadcastPing();

            RALeaderboardResponse response = new RALeaderboardResponse() {
                Success = true,
                Score = score,
                BestScore = userGameData.LeaderboardEntries.Select(r => r.Value.Score).Max(),
                RankInfo = new RALeaderboardResponse.RankObject() {
                    NumEntries = 1, // out of scope
                    Rank = 1
                },
                TopEntries = new RALeaderboardResponse.TopObject[0] // out of scope
            };

            await ctx.Response.SendJson(response);
        }

        internal static async Task LaheeInfo(HttpContextBase ctx) {

            LaheeResponse response = new LaheeResponse() {
                version = Program.NAME,
                games = StaticDataManager.GetAllGameData(),
                users = UserManager.GetAllUserData(),
            };

            await ctx.Response.SendJson(response);
        }

        internal static async Task LaheeUserInfo(HttpContextBase ctx) {

            String user = ctx.Request.GetParameter("user");
            int gameid = Int32.Parse(ctx.Request.GetParameter("gameid"));

            UserData userData = UserManager.GetUserData(user);
            if (userData == null) {
                await ctx.Response.SendJson(new RAErrorResponse("User does not exist!"));
                return;
            }

            GameData game = StaticDataManager.FindGameDataById(gameid);
            if (game == null) {
                await ctx.Response.SendJson(new RAErrorResponse("Game ID is not registered!"));
                return;
            }

            UserGameData userGameData = userData.GameData[gameid];

            LaheeUserResponse response = new LaheeUserResponse() {
                currentgameid = userData.CurrentGameId,
                gamestatus = userGameData?.LastPresence,
                lastping = userGameData?.PlayTimeLastPing,
                playtime = userGameData?.PlayTimeApprox,
                achievements = userGameData.Achievements
            };

            await ctx.Response.SendJson(response);
        }
    }
}