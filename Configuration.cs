using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE {
    internal class Configuration {

        public static IConfigurationRoot Current { get; private set; }

        public static void Initialize() {
            Current = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .AddJsonFile("appsettings.debug.json", true)
                .Build();
        }

        public static string Get(string section, string value) {
            return Current.GetSection(section)?.GetSection(value)?.Value;
        }
    }
}
