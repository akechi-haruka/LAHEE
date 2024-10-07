using LAHEE.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE {

    internal class RAErrorResponse {
        public bool Success;
        public string Error;
        public int Code;

        public RAErrorResponse(string error) {
            Error = error;
        }
    }

    internal class RALoginResponse {
        public bool Success;
        public string User;
        public string Token;
        public int Score;
        public int SoftcoreScore;
        public int Messages;
        public int Permissions;
        public string AccountType;
        public string DisplayName;
    }

    internal class RAGameIDResponse {
        public bool Success;
        public int GameID;
    }

    internal class RAPatchResponse {
        public bool Success;
        public GameData PatchData;
    }

    internal class RAStartSessionResponse {
        public bool Success;
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

    internal class RAUnlockResponse {
        public bool Success;
        public int Score;
        public int SoftcoreScore;
        public int AchievementID;
        public int AchievementsRemaining;
    }

    internal class RALeaderboardResponse {
        public bool Success;
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
}
