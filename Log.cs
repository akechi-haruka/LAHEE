using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;

namespace LAHEE;

class Log {
    public static ILogger Main { get; private set; }
    public static ILogger Network { get; private set; }
    public static ILogger Data { get; private set; }
    public static ILogger User { get; private set; }
    public static ILogger RCheevos { get; private set; }
    public static ILogger Websocket { get; private set; }

    public static void Initialize() {
        IConfigurationSection loggingConfig = Configuration.Current.GetSection("Logging");

        ILoggerFactory factory = LoggerFactory.Create(builder => builder
            .AddConfiguration(loggingConfig)
            .AddSimpleConsole(options => { options.SingleLine = true; })
            .AddDebug()
            .AddFile(loggingConfig.GetSection("File"))
        );
        Main = factory.CreateLogger("Main");
        Network = factory.CreateLogger("Net ");
        Data = factory.CreateLogger("Data");
        User = factory.CreateLogger("User");
        RCheevos = factory.CreateLogger("Rche");
        Websocket = factory.CreateLogger("Webs");

        Main.LogInformation("Logging started.");
        Main.LogInformation("Local Achievements Home Enhanced Edition " + Program.NAME);
    }
}