using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System.Reflection;
using System.Text;

namespace LAHEE {
    class Program {
        
        internal static readonly char[] INVALID_FILE_NAME_CHARS = Path.GetInvalidFileNameChars();

        public static readonly String NAME;

        static Program() {

            string gitHash = Assembly.Load(typeof(Program).Assembly.FullName)
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

            AssemblyName assemblyInfo = Assembly.GetExecutingAssembly().GetName();
            NAME = assemblyInfo.Name + "/" + assemblyInfo.Version + "-" + gitHash + " - Akechi Haruka";
        }

        private static void Main(string[] args) {

            Console.Title = NAME;

            String path = System.Environment.ProcessPath;
            if (path != null) {
                Environment.CurrentDirectory = Path.GetDirectoryName(path);
            }

            try {
                Configuration.Initialize();
            } catch (Exception ex) {
                Console.WriteLine("An error ocurred during loading the configuration:\n" + ex.Message);
#if DEBUG
                Console.WriteLine(ex);
#endif
                Console.ReadLine();
                return;
            }

            string badgeDirectory = Configuration.Get("LAHEE", "BadgeDirectory");
            if (!File.Exists(badgeDirectory)) {
                Directory.CreateDirectory(badgeDirectory);
            }

            Log.Initialize();
            UserManager.Initialize();
            StaticDataManager.Initialize();
            Network.Initialize();
            LiveTicker.Initialize();
            CaptureManager.Initialize();

            Log.Main.LogInformation("Initialization complete.");
            Console.WriteLine("Type \"stop\" to save and exit.\nType \"help\" for console commands.\nPoint your emulator to: " + Network.LOCAL_URL);


            Console.CancelKeyPress += Console_CancelKeyPress;

            while (true) {

                string line = Console.ReadLine();

                if (line == null) {
                    break;
                }
                if (String.IsNullOrWhiteSpace(line)) {
                    continue;
                }

                Log.Main.LogInformation("Executed command: {cmd}", line);

                try {
                    ExecuteConsoleCommand(ParseConsoleCommand(line));
                }catch(Exception ex) {
                    Log.Main.LogError("Error executing console command: {e}", ex);
                }
            }
        }

        private static void ExecuteConsoleCommand(string[] args) {
            switch (args[0]) {
                case "help":
                    Console.WriteLine(@"
help                                                                  Show this help
exit                                                                  Exit LAHEE
stop                                                                  Exit LAHEE
listach <gamename>                                                    Lists all achievements for game
unlock <username> <gamename> <achievementname> <hardcore 1/0>         Grant an achievement
lock <username> <gamename> <achievementname> <hardcore 1/0>           Remove an achievement
lockall <username> <gamename>                                         Remove ALL achievements
fetch <gameid> [override_gameid] [unofficial 1/0] [copy_unlocks_to]   Copies game and achievement data from official server
reload                                                                Reloads achievement data
reloaduser                                                            Reloads user data
");
                    break;
                case "exit":
                case "quit":
                case "stop":
                    Console_CancelKeyPress(null, null);
                    break;
                case "listach":
                    ListAchievementsFromConsole(args[1]);
                    break;
                case "unlock":
                    UnlockAchievementFromConsole(args[1], args[2], args[3], args[4] == "1", true);
                    break;
                case "lock":
                    UnlockAchievementFromConsole(args[1], args[2], args[3], false, false);
                    break;
                case "lockall":
                    LockAllAchievementsFromConsole(args[1], args[2]);
                    break;
                case "reload":
                    ReloadFromConsole();
                    break;
                case "reloaduser":
                    ReloadUserFromConsole();
                    break;
                case "fetch":
                    RaOfficialServer.FetchData(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 && args[3] == "1", args.Length >= 5 ? args[4] : null);
                    break;
                default:
                    Log.Main.LogWarning("Unknown command: {arg}", args[0]);
                    break;
            }
        }

        private static void ReloadUserFromConsole() {
            UserManager.Load(UserManager.UserDataDirectory);
            Log.Main.LogInformation("Reload completed");
        }

        private static void ReloadFromConsole() {
            StaticDataManager.InitializeAchievements();
            Log.Main.LogInformation("Reload completed");
        }

        private static void LockAllAchievementsFromConsole(string username, string gamename) {
            UserData user = UserManager.GetUserData(username);
            if (user == null) {
                Log.Main.LogError("User not found.");
                return;
            }
            GameData game = StaticDataManager.FindGameDataByName(gamename, true);
            if (game == null) {
                Log.Main.LogError("Game not found.");
                return;
            }

            user.GameData[game.ID].Achievements.Clear();

            Log.Main.LogInformation("Successfully removed all achievements of \"{game}\" for {user}", game, user);
            UserManager.Save();
        }

        private static void ListAchievementsFromConsole(string gamename) {
            GameData game = StaticDataManager.FindGameDataByName(gamename, true);
            if (game == null) {
                Log.Main.LogError("Game not found.");
                return;
            }

            foreach (AchievementData ach in game.Achievements) {
                Log.Main.LogInformation("{a}", ach);
            }
        }

        private static void UnlockAchievementFromConsole(string username, string gamename, string achievementname, bool hardcore, bool unlock) {
            UserData user = UserManager.GetUserData(username);
            if (user == null) {
                Log.Main.LogError("User not found.");
                return;
            }
            GameData game = StaticDataManager.FindGameDataByName(gamename, true);
            if (game == null) {
                Log.Main.LogError("Game not found.");
                return;
            }
            AchievementData ach = game.GetAchievementByName(achievementname, true);
            if (ach == null) {
                Log.Main.LogError("Achievement not found.");
                return;
            }
            if (!user.GameData.TryGetValue(game.ID, out UserGameData userGameData)) {
                Log.Main.LogError("User has no data recorded for this game.");
                return;
            }

            UserAchievementData userAchievementData;
            if (unlock) {
                userAchievementData = userGameData.UnlockAchievement(ach.ID, hardcore);

                LiveTicker.BroadcastUnlock(game.ID, userAchievementData);
                CaptureManager.StartCapture(game, user, ach);
            } else {
                if (!userGameData.Achievements.TryGetValue(ach.ID, out userAchievementData)) {
                    Log.Main.LogError("User does not have this achievement.");
                    return;
                }

                userAchievementData.AchieveDateSoftcore = 0;
                userAchievementData.AchieveDate = 0;
                userAchievementData.AchievePlaytime = TimeSpan.Zero;
                userAchievementData.AchievePlaytimeSoftcore = TimeSpan.Zero;
                userAchievementData.Status = UserAchievementData.StatusFlag.Locked;
            }

            Log.Main.LogInformation("Successfully set achievement \"{ach}\" of \"{game}\" for {user} to {status}", ach, game, user, userAchievementData?.Status);
            UserManager.Save();
            
            LiveTicker.BroadcastPing();
        }

        private static string[] ParseConsoleCommand(string line) {
            string[] args;
            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(line))) {
                using (TextFieldParser tfp = new TextFieldParser(ms)) {
                    tfp.Delimiters = new string[] { " " };
                    tfp.HasFieldsEnclosedInQuotes = true;
                    args = tfp.ReadFields();
                }
            }
            return args;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
            Log.Main.LogInformation("Requested closing!");
            Network.Stop();
            UserManager.Save();
            StaticDataManager.SaveAllCommentFiles();
            Environment.Exit(0);

        }
    }

}
