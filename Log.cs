using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LAHEE {
    internal class Log {

        public static ILogger Main { get; private set; }
        public static ILogger Network { get; private set; }
        public static ILogger Data { get; private set; }
        public static ILogger User { get; private set; }

        public static void Initialize() {

            IConfigurationSection loggingConfig = Configuration.Current.GetSection("Logging");

            ILoggerFactory factory = LoggerFactory.Create(builder => builder
                .AddConfiguration(loggingConfig)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                })
                .AddDebug()
                .AddFile(loggingConfig.GetSection("File"))
            );
            Main = factory.CreateLogger("Main");
            Network = factory.CreateLogger("Network");
            Data = factory.CreateLogger("Data");
            User = factory.CreateLogger("User");

            Main.LogInformation("Logging started.");
            Main.LogInformation("Local Achievements Home Enhanced Edition " + Program.NAME);
        }

    }
}
