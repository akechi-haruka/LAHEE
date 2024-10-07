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
        public Dictionary<int, UserLeaderboardData> LeaderboardEntries;
    }
}
