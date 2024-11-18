using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace LAHEE {

    internal class Program {

        public static readonly String NAME;

        static Program() {

            string gitHash = Assembly.Load(typeof(Program).Assembly.FullName)
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

            AssemblyName assemblyInfo = Assembly.GetExecutingAssembly().GetName();
            NAME = assemblyInfo.Name + "/" + assemblyInfo.Version + "-" + gitHash + " - Akechi Haruka";
        }

        static void Main(string[] args) {

            Console.Title = NAME;

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

            Log.Initialize();
            UserManager.Initialize();
            StaticDataManager.Initialize();
            Network.Initialize();
            LiveTicker.Initialize();

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
help                                                            Show this help
exit                                                            Exit LAHEE
stop                                                            Exit LAHEE
listach <gamename>                                              Lists all achievements for game
unlock <username> <gamename> <achievementname> <hardcore 1/0>   Grant an achievement
lock <username> <gamename> <achievementname> <hardcore 1/0>     Remove an achievement
lockall <username> <gamename>                                   Remove ALL achievements
reload                                                          Reloads achievement data
reloaduser                                                      Reloads user data
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
                    UnlockAchievementFromConsole(args[1], args[2], args[3], args[4] == "1", false);
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
                Console.WriteLine(ach);
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
            if (!userGameData.Achievements.TryGetValue(ach.ID, out UserAchievementData userAchievementData)){
                userAchievementData = new UserAchievementData() {
                    AchievementID = ach.ID
                };
                userGameData.Achievements.Add(ach.ID, userAchievementData);
            }

            if (unlock) {
                if (hardcore) {
                    userAchievementData.Status = UserAchievementData.StatusFlag.HardcoreUnlock;
                    userAchievementData.AchieveDate = Util.CurrentUnixSeconds;
                } else {
                    userAchievementData.Status = UserAchievementData.StatusFlag.SoftcoreUnlock;
                    userAchievementData.AchieveDateSoftcore = Util.CurrentUnixSeconds;
                }
            } else {
                userAchievementData.Status = UserAchievementData.StatusFlag.Locked;
                userAchievementData.AchieveDate = 0;
                userAchievementData.AchieveDateSoftcore = 0;
            }

            Log.Main.LogInformation("Successfully set achievement \"{ach}\" of \"{game}\" for {user} to {status}", ach, game, user, userAchievementData.Status);
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
            Environment.Exit(0);

        }
    }

}
