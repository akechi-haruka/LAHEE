using System.Reflection;
using System.Text;
using Haruka.Common.Configuration;
using LAHEE.Data;
using LAHEE.Util;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace LAHEE;

class Program {
    internal static readonly char[] INVALID_FILE_NAME_CHARS = Path.GetInvalidFileNameChars();

    public static readonly string NAME;

    public static AppConfig Config;
    public static List<string> Notifications = new List<string>();

    static Program() {
        string gitHash = Assembly.Load(typeof(Program).Assembly.FullName!)
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

        AssemblyName assemblyInfo = Assembly.GetExecutingAssembly().GetName();
        NAME = assemblyInfo.Name + "/" + assemblyInfo.Version + "-" + gitHash + " - Akechi Haruka";
    }

    private static void Main() {
        Console.Title = NAME;

        string path = Environment.ProcessPath;
        if (path != null) {
            Environment.CurrentDirectory = Path.GetDirectoryName(path)!;
        }

        try {
            AppConfig.Initialize();
            Config = new AppConfig("LAHEE");
        } catch (Exception ex) {
            Console.WriteLine("An error ocurred during loading the configuration:\n" + ex.Message);
#if DEBUG
            Console.WriteLine(ex);
#endif
            Console.ReadLine();
            return;
        }

        string badgeDirectory = Config.Get("LAHEE", "BadgeDirectory") ?? "Badge";
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
        Console.WriteLine("Type \"stop\" to save and exit.\nType \"help\" for console commands.\nPoint your emulator to: " + Network.LocalUrl);

        if (Config.GetBool("LAHEE", "AutoOpenBrowser")) {
            Log.Main.LogInformation("Opening your web browser...");
            try {
                Utils.OpenBrowser(Network.LocalUrl);
            } catch (Exception ex) {
                Log.Main.LogError("Failed to open web browser: " + ex.Message);
            }
        }

        if (Config.GetBool("LAHEE", "RAFetch", "CheckSetRevisions")) {
            new Thread(RAOfficialServer.FetchUpdatedSets) {
                Name = "Check Set Revisions"
            }.Start();
        }

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
            } catch (Exception ex) {
                Log.Main.LogError("Error executing console command: {e}", ex);
            }
        }
    }

    private static void ExecuteConsoleCommand(string[] args) {
        switch (args[0]) {
            case "help":
                Console.WriteLine(@"
help                                                                              Show this help
exit                                                                              Exit LAHEE
stop                                                                              Exit LAHEE
listach <gamename>                                                                Lists all achievements for game
unlock <username> <gamename> <achievementname> <hardcore 1/0> [date] [playtime]   Grant an achievement
lock <username> <gamename> <achievementname> <hardcore 1/0>                       Remove an achievement
lockall <username> <gamename>                                                     Remove ALL achievements
fetch <gameid> [override_gameid/0] [unofficial 1/0] [force 1/0] [copy_unlocks_to] Copies game and achievement data from official server
delete <gamename>                                                                 Deletes game and achievement data (not user data!)
addhash <gamename> <hash>                                                         Adds a ROM hash to a game
reload                                                                            Reloads achievement data
reloaduser                                                                        Reloads user data
");
                break;
            case "exit":
            case "quit":
            case "stop":
                Console_CancelKeyPress(null, null);
                break;
            case "listach":
                if (args.Length == 2) {
                    ListAchievementsFromConsole(args[1]);
                } else {
                    Log.Main.LogError("Command requires exactly one argument.");
                }

                break;
            case "unlock":
                if (args.Length >= 5) {
                    UnlockAchievementFromConsole(args[1], args[2], args[3], args[4] == "1", true, args.Length >= 6 ? args[5] : null, args.Length >= 7 ? args[6] : null);
                } else {
                    Log.Main.LogError("Command requires at least 4 arguments.");
                }

                break;
            case "lock":
                if (args.Length == 4) {
                    UnlockAchievementFromConsole(args[1], args[2], args[3], false, false);
                } else {
                    Log.Main.LogError("Command requires exactly 3 arguments.");
                }

                break;
            case "lockall":
                if (args.Length == 2) {
                    LockAllAchievementsFromConsole(args[1], args[2]);
                } else {
                    Log.Main.LogError("Command requires exactly 2 arguments.");
                }

                break;
            case "reload":
                ReloadFromConsole();
                break;
            case "reloaduser":
                ReloadUserFromConsole();
                break;
            case "fetch":
                if (args.Length >= 2) {
                    RAOfficialServer.FetchData(args[1], args.Length >= 3 && args[2] != "0" ? args[2] : null, args.Length >= 4 && args[3] == "1", args.Length >= 5 && args[4] == "1", args.Length >= 6 ? args[5] : null);
                } else {
                    Log.Main.LogError("Command requires at least one argument.");
                }

                break;
            case "delete":
                DeleteDataFromConsole(args[1]);
                break;
            case "addhash":
                if (args.Length == 3) {
                    AddHashFromConsole(args[1], args[2]);
                } else {
                    Log.Main.LogError("Command requires exactly 2 arguments.");
                }

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

    private static void LockAllAchievementsFromConsole(string username, string gameName) {
        UserData user = UserManager.GetUserData(username);
        if (user == null) {
            Log.Main.LogError("User not found.");
            return;
        }

        GameData game = StaticDataManager.FindGameDataByName(gameName, false);
        if (game == null) {
            game = StaticDataManager.FindGameDataByName(gameName, true);
            if (game == null) {
                Log.Main.LogError("Game not found: " + gameName);
                return;
            }
        }

        user.GameData[game.ID].Achievements.Clear();

        Log.Main.LogInformation("Successfully removed all achievements of \"{game}\" for {user}", game, user);
        UserManager.Save();
    }

    private static void ListAchievementsFromConsole(string gamename) {
        GameData game = StaticDataManager.FindGameDataByName(gamename, false);
        if (game == null) {
            game = StaticDataManager.FindGameDataByName(gamename, true);
            if (game == null) {
                Log.Main.LogError("Game not found: " + gamename);
                return;
            }
        }

        foreach (AchievementData ach in game.GetAllAchievements()) {
            Log.Main.LogInformation("{a}", ach);
        }
    }

    private static void UnlockAchievementFromConsole(string username, string gamename, string achievementName, bool hardcore, bool unlock, string unlockTime = null, string unlockPlayTime = null) {
        UserData user = UserManager.GetUserData(username);
        if (user == null) {
            Log.Main.LogError("User not found: " + username);
            return;
        }

        GameData game = StaticDataManager.FindGameDataByName(gamename, false);
        if (game == null) {
            game = StaticDataManager.FindGameDataByName(gamename, true);
            if (game == null) {
                Log.Main.LogError("Game not found: " + gamename);
                return;
            }
        }

        AchievementData ach = game.GetAchievementByName(achievementName, false);
        if (ach == null) {
            ach = game.GetAchievementByName(achievementName, true);
            if (ach == null) {
                Log.Main.LogError("Achievement not found (in " + game + "): " + achievementName);
                return;
            }
        }

        if (!user.GameData.TryGetValue(game.ID, out UserGameData userGameData)) {
            Log.Main.LogError("User has no data recorded for this game.");
            return;
        }

        UserAchievementData userAchievementData;
        if (unlock) {
            long unlockUnixSeconds = 0;
            TimeSpan? unlockPlayTimeSpan = null;

            if (unlockTime != null) {
                if (DateTime.TryParse(unlockTime, out DateTime unlockDateTime)) {
                    unlockUnixSeconds = (long)Utils.ConvertToUnixTimestamp(unlockDateTime);
                } else {
                    Log.Main.LogError("Could not parse given achievement unlock date.");
                    return;
                }
            }

            if (unlockPlayTime != null) {
                if (TimeSpan.TryParse(unlockPlayTime, out TimeSpan unlockPlayTimeSpanParsed)) {
                    unlockPlayTimeSpan = unlockPlayTimeSpanParsed;
                } else {
                    Log.Main.LogError("Could not parse given achievement unlock play time.");
                    return;
                }
            }

            userAchievementData = userGameData.UnlockAchievement(ach.ID, hardcore, unlockUnixSeconds, unlockPlayTimeSpan);

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

        LiveTicker.BroadcastPing(LiveTicker.LiveTickerEventPing.PingType.AchievementUnlock);
    }

    private static void DeleteDataFromConsole(string gameName) {
        GameData game = StaticDataManager.FindGameDataByName(gameName, false);
        if (game == null) {
            game = StaticDataManager.FindGameDataByName(gameName, true);
            if (game == null) {
                Log.Main.LogError("Game not found: " + gameName);
                return;
            }
        }

        int count = 0;
        Log.Data.LogInformation("Deleting all files belonging to: {g}", game);
        string dir = Config.Get("LAHEE", "DataDirectory");
        foreach (string file in Directory.EnumerateFiles(dir)) {
            if (StaticDataManager.GetGameIdFromFilePath(file) == game.ID) {
                Log.Data.LogInformation("Deleting: {f}", file);
                File.Delete(file);
                count++;
            }
        }

        Log.Data.LogInformation("Deleted {n} file(s)", count);

        StaticDataManager.InitializeAchievements();
    }

    private static void AddHashFromConsole(string gameName, string hash) {
        GameData game = StaticDataManager.FindGameDataByName(gameName, true);
        if (game == null) {
            Log.Main.LogError("Game not found.");
            return;
        }

        string dir = Config.Get("LAHEE", "DataDirectory");
        string fn = Path.Combine(dir, game.ID + "-CustomHashes.hash.txt");
        string content = "";

        if (File.Exists(fn)) {
            content = File.ReadAllText(fn);
        }

        content += "\r\n" + hash;

        File.WriteAllText(fn, content);
        game.ROMHashes.Add(hash);

        Log.Data.LogInformation("Added {h} to {g}.", hash, game);
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
        StaticDataManager.SaveGlobalData();
        Environment.Exit(0);
    }

    public static void AddNotification(string message) {
        Notifications.Add(message);
        LiveTicker.BroadcastNotification(message);
    }
}