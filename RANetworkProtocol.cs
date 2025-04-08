using LAHEE.Data;

namespace LAHEE {
    class RAAnyResponse {
        public bool Success;
    }

    class RAErrorResponse : RAAnyResponse {
        public string Error;
        public int Code;

        public RAErrorResponse(string error) {
            Error = error;
        }
    }

    class RALoginResponse : RAAnyResponse {
        public string User;
        public string Token;
        public int Score;
        public int SoftcoreScore;
        public int Messages;
        public int Permissions;
        public string AccountType;
        public string DisplayName;
    }

    class RAGameIDResponse : RAAnyResponse {
        public int GameID;
    }

    class RAPatchResponse : RAAnyResponse {
        public GameData PatchData;
    }

    class RAStartSessionResponse : RAAnyResponse {
        public RAStartSessionAchievementData[] Unlocks;
        public RAStartSessionAchievementData[] HardcoreUnlocks;
        public long ServerNow;

        public class RAStartSessionAchievementData {
            public int ID;
            public long When;

            public RAStartSessionAchievementData(UserAchievementData userAchievement, bool isHardcore) {
                ID = userAchievement.AchievementID;
                When = isHardcore ? userAchievement.AchieveDate : userAchievement.AchieveDateSoftcore;
            }
        }
    }

    class RAAchievementListResponse : RAAnyResponse {
        public int[] UserUnlocks;
        public int GameId;
        public bool HardcoreMode;
    }

    class RAUnlockResponse : RAAnyResponse {
        public int Score;
        public int SoftcoreScore;
        public int AchievementID;
        public int AchievementsRemaining;
    }

    class RALeaderboardResponse : RAAnyResponse {
        public int Score;
        public int BestScore;
        public RankObject RankInfo;
        public TopObject[] TopEntries;

        public class RankObject {
            public int Rank;
            public int NumEntries;
        }

        public class TopObject {
            public String User;
            public int Rank;
            public int Score;
        }
    }

    class LaheeResponse {
        public String version;
        public UserData[] users;
        public GameData[] games;
    }

    class LaheeUserResponse {
        public int currentgameid;
        public DateTime? lastping;
        public TimeSpan? playtime;
        public String gamestatus;
        public Dictionary<int, UserAchievementData> achievements;
    }

    class RAApiHashesResponse {
        public Hash[] Results;

        internal class Hash {
            public String MD5;
            public String Name;
            public String[] Labels;
            public String PatchUrl;
        }
    }
}
