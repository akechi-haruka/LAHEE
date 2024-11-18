
using LAHEE.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LAHEE {
    internal class UserManager {

        public static string UserDataDirectory { get; private set; }

        private static Dictionary<string, UserData> userData;
        private static Dictionary<string, UserData> activeTokens;

        internal static void Initialize() {
            UserDataDirectory = Configuration.Get("LAHEE", "UserDirectory");

            activeTokens = new Dictionary<string, UserData>();

            Load(UserDataDirectory);

            Log.User.LogInformation("Finished loading data: {users} User(s) with {achiev} Achievements total", userData.Count, userData.Sum((r) => r.Value.GameData?.Sum((ru => ru.Value.Achievements.Count))));
        }

        public static void Load(string dir) {
            userData = new Dictionary<string, UserData>();

            Log.User.LogInformation("Loading user profiles from {Dir}...", dir);

            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
                Log.User.LogTrace("Created directory");
            }

            foreach (string file in Directory.GetFiles(dir)) {
                String username = Path.GetFileNameWithoutExtension(file);
                try {
                    Log.User.LogDebug("Reading {f}", file);

                    UserData data = JsonConvert.DeserializeObject<UserData>(File.ReadAllText(file));
                    userData[data.UserName] = data;

                    Log.User.LogDebug("Loaded data for \"{User}\"", data);
                } catch (Exception ex) {
                    Log.User.LogError("Failed to load data from " + file + ": " + ex);
                    userData[username] = new UserData() {
                        UserName = username,
                        AllowUse = false
                    };
                }
            }
        }

        public static UserData GetUserData(string username) {
            if (userData.ContainsKey(username)) {
                return userData[username];
            } else {
                return null;
            }
        }

        public static UserData RegisterNewUser(string username) {
            UserData user = new UserData() {
                AllowUse = true,
                UserName = username,
                ID = new Random().Next(),
                GameData = new Dictionary<int, UserGameData>()
            };
            userData.Add(username, user);
            Log.User.LogInformation("Registered new user: {User}", user);
            return user;
        }

        public static void Save() {
            Save(UserDataDirectory);
        }

        public static void Save(string dir) {

            foreach (UserData data in userData.Values) {
                if (data.AllowUse) {
                    File.WriteAllText(Path.Combine(dir, data.UserName + ".json"), JsonConvert.SerializeObject(data));
                    Log.User.LogDebug("Saved user data for " + data.UserName);
                } else {
                    Log.User.LogWarning("Not saving {User}, because data loading has failed!", data);
                }
            }

            Log.User.LogInformation("User data was saved");

        }

        public static string RegisterSessionToken(UserData user) {
            Log.User.LogDebug("Registering random session token");
            return RegisterSessionToken(user, Util.RandomString(32));
        }

        public static string RegisterSessionToken(UserData user, String token) {
            Log.User.LogDebug("Registering session token: {token}", token);
            activeTokens[token] = user;
            return token;
        }

        public static UserData GetUserDataFromToken(string str) {
            return activeTokens.GetValueOrDefault(str, null);
        }

        internal static UserData[] GetAllUserData() {
            return userData.Values.ToArray();
        }
    }
}