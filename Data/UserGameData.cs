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

        public void UnlockAchievement(int achievementId, bool isHardcore, long achieveDate = 0) {

            if (achievementId == StaticDataManager.UNSUPPORTED_EMULATOR_ACHIEVEMENT_ID) { // "Unsupported Emulator"
                return;
            }

            if (!Achievements.TryGetValue(achievementId, out UserAchievementData userAchievementData)) {
                userAchievementData = new UserAchievementData() {
                    AchievementID = achievementId
                };
                Achievements[achievementId] = userAchievementData;
            }

            if (isHardcore) {
                userAchievementData.Status = UserAchievementData.StatusFlag.HardcoreUnlock;
                
                if (userAchievementData.AchieveDate == 0) {
                    userAchievementData.AchieveDate = achieveDate > 0 ? achieveDate : Util.CurrentUnixSeconds;
                }

                if (userAchievementData.AchievePlaytime == TimeSpan.Zero) {
                    userAchievementData.AchievePlaytime = PlayTimeApprox + (DateTime.Now - PlayTimeLastPing);
                }
            } else if (userAchievementData.Status == UserAchievementData.StatusFlag.Locked) {
                userAchievementData.Status = UserAchievementData.StatusFlag.SoftcoreUnlock;
                
                if (userAchievementData.AchieveDateSoftcore == 0) {
                    userAchievementData.AchieveDateSoftcore = achieveDate > 0 ? achieveDate : Util.CurrentUnixSeconds;
                }

                if (userAchievementData.AchievePlaytimeSoftcore == TimeSpan.Zero) {
                    userAchievementData.AchievePlaytimeSoftcore = PlayTimeApprox + (DateTime.Now - PlayTimeLastPing);
                }
            }
        }
    }
}
