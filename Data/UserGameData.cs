using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE.Data {

    public class UserGameData {
        public int GameID;
        public String LastPresence;
        public Dictionary<int, UserAchievementData> Achievements;
        public Dictionary<int, List<UserLeaderboardData>> LeaderboardEntries;
        public List<PresenceHistory> PresenceHistory;
        public DateTime FirstPlay;
        public DateTime LastPlay;
        public DateTime PlayTimeLastPing;
        public TimeSpan PlayTimeApprox;
    }
}
