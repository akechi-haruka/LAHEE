using Microsoft.Extensions.Configuration;

namespace LAHEE;

class Configuration {
    public static IConfigurationRoot Current { get; private set; }

    public static void Initialize() {
        Current = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", false)
            .AddJsonFile("appsettings.debug.json", true, true)
            .AddJsonFile("appsettings.local.json", true, true)
            .Build();
    }

    public static string Get(string section, string value) {
        return Current.GetSection(section)?.GetSection(value)?.Value;
    }

    public static string Get(string section, string subsection, string value) {
        return Current.GetSection(section)?.GetSection(subsection)?.GetSection(value)?.Value;
    }

    public static int GetInt(string section, string value) {
        return (Current.GetSection(section)?.GetValue<int>(value)).Value;
    }

    public static bool GetBool(string section, string value) {
        return (Current.GetSection(section)?.GetValue<bool>(value)).Value;
    }

    public static bool GetBool(string section, string subsection, string value) {
        return (Current.GetSection(section)?.GetSection(subsection)?.GetValue<bool>(value)).Value;
    }
}