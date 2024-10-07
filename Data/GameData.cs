using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE.Data {
    public class GameData {
        public int ID;
        public String Title;
        public String ImageIcon;
        public String RichPresencePatch;
        public int ConsoleID;
        public String ImageIconURL;
        public AchievementData[] Achievements;
        public LeaderboardData[] Leaderboards;
        public List<String> ROMHashes = new List<string>();

        public override string ToString() {
            return Title + " (" + ID + ")";
        }

        public AchievementData GetAchievementById(int achievementid) {
            return Achievements.Where(a => a.ID == achievementid).FirstOrDefault((AchievementData)null);
        }

        internal LeaderboardData GetLeaderboardById(int leaderboardId) {
            return Leaderboards.Where(l => l.ID == leaderboardId).FirstOrDefault((LeaderboardData)null);
        }
    }
}
