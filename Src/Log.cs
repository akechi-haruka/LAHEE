using Microsoft.Extensions.Logging;

namespace LAHEE;

static class Log {
    public static ILogger Main { get; private set; }
    public static ILogger Network { get; private set; }
    public static ILogger Data { get; private set; }
    public static ILogger User { get; private set; }
    public static ILogger RCheevos { get; private set; }
    public static ILogger Websocket { get; private set; }

    public static void Initialize() {
        Haruka.Common.Log.Initialize();
        Main = Haruka.Common.Log.GetOrCreate("Main");
        Network = Haruka.Common.Log.GetOrCreate("Net ");
        Data = Haruka.Common.Log.GetOrCreate("Data");
        User = Haruka.Common.Log.GetOrCreate("User");
        RCheevos = Haruka.Common.Log.GetOrCreate("Rche");
        Websocket = Haruka.Common.Log.GetOrCreate("Webs");

        Main.LogInformation("Local Achievements Home Enhanced Edition " + Program.NAME);
    }
}