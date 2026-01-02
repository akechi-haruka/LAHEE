using System.Drawing;
using System.Drawing.Imaging;
using LAHEE.Data;
using LAHEE.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LAHEE;

public static class CaptureManager {
    public enum CaptureTrigger {
        Invalid,
        Screenshot,
        OBSWebsocket
    }

    public class CaptureAction {
        public CaptureTrigger Trigger;
        public int Delay;
        public String Parameter;
    }

    public const String DIRECTORY = "Capture";

    private static List<CaptureAction> actions;

    public static void Initialize() {
        if (!Directory.Exists(DIRECTORY)) {
            Directory.CreateDirectory(DIRECTORY);
        }

        actions = new List<CaptureAction>();
        IEnumerable<IConfigurationSection> ca = Configuration.Current.GetSection("LAHEE").GetSection("Capture").GetChildren();
        foreach (IConfigurationSection c in ca) {
            CaptureAction a = new CaptureAction() {
                Delay = Int32.Parse(c.GetSection("Delay").Value ?? "0"),
                Trigger = Enum.Parse<CaptureTrigger>(c.GetSection("Trigger").Value ?? "Invalid"),
                Parameter = c.GetSection("Parameter").Value
            };
            actions.Add(a);
        }

        if (actions.Any(a => a.Trigger == CaptureTrigger.OBSWebsocket)) {
            Log.Main.LogInformation("OBSWebsocket capture trigger found, checking connection...");
            Task<bool> task = DoWebsocketAsync("GetVersion");
            task.Wait();
            if (!task.Result) {
                Log.Main.LogError("OBSWebsocket capture trigger enabled, but failed to test OBS websocket connectivity. Disabling OBSWebsocket captures!");
                actions.RemoveAll(a => a.Trigger == CaptureTrigger.OBSWebsocket);
            }
        }

        Log.Main.LogInformation("{n} capture actions configured.", actions.Count);
    }

    private static String SanitizePath(string path) {
        foreach (char c in Path.GetInvalidFileNameChars()) {
            path = path.Replace(c, '-');
        }

        return path;
    }

    public static void StartCapture(GameData game, UserData user, AchievementData ach) {
        if (actions.Count == 0) {
            return;
        }

        Log.Main.LogDebug("Starting Capture thread for " + user + " / " + game + " / " + ach);
        new Thread(() => StartCaptureT(game, user, ach)) {
            Name = "Capture Thread: " + user + " / " + game + " / " + ach
        }.Start();
    }

    private static void StartCaptureT(GameData game, UserData user, AchievementData ach) {
        int time = 0;
        foreach (CaptureAction a in actions) {
            Log.Main.LogTrace("Wait {n}", a.Delay - time);
            Thread.Sleep(Math.Max(a.Delay - time, 0));
            time += a.Delay;
            DoCaptureAction(a.Trigger, a.Parameter, game, user, ach);
        }

        Log.Main.LogDebug("Capture thread finished for " + user + " / " + game + " / " + ach);
    }

    private static void DoCaptureAction(CaptureTrigger trigger, string parameter, GameData game, UserData user, AchievementData ach) {
        if (trigger == CaptureTrigger.Screenshot) {
            DoScreenshot(parameter, game, user, ach);
        } else if (trigger == CaptureTrigger.OBSWebsocket) {
            DoWebsocketAsync(parameter).Wait();
        } else {
            Log.Main.LogError("Unknown capture action: {a}", trigger);
        }
    }

    private static async Task<bool> DoWebsocketAsync(string parameter) {
        Log.Main.LogDebug("Connecting to OBS websocket...");

        Uri uri = new Uri(Configuration.Get("LAHEE", "OBSWebsocketUrl"));
        return await new OBSWebsocket(uri).ConnectAndSendAsync(parameter);
    }

    private static void DoScreenshot(string parameter, GameData game, UserData user, AchievementData ach) {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) {
            Log.Main.LogError("Screenshot feature is only supported on Windows.");
            return;
        }

        String path = Path.Combine(DIRECTORY, SanitizePath(user.UserName), SanitizePath(game.Title), SanitizePath(DateTime.Now + "_" + ach.Title + ".png"));

        try {
            String parent = Directory.GetParent(path)?.FullName;
            if (parent == null) {
                throw new ArgumentException("Invalid path: " + path);
            }

            if (!Directory.Exists(parent)) {
                Directory.CreateDirectory(parent);
            }

            Bitmap image;
            switch (parameter) {
                case "Desktop":
                    image = ScreenCapture.CaptureDesktop();
                    break;
                case "Window":
                    image = ScreenCapture.CaptureActiveWindow();
                    break;
                default:
                    Log.Main.LogWarning("Unknown parameter for screenshot capture: {p}, defaulting to Desktop.", parameter);
                    image = ScreenCapture.CaptureDesktop();
                    break;
            }

            image.Save(path, ImageFormat.Png);
            Log.Main.LogInformation("Screenshot saved to: {p}", path);
        } catch (Exception ex) {
            Log.Main.LogCritical(ex, "Failed creating screenshot to {p}", path);
        }
    }
}