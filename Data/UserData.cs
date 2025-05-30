﻿namespace LAHEE.Data {
    public class UserData {

        public int ID;
        public bool AllowUse;
        public String UserName;
        public Dictionary<int, UserGameData> GameData;
        public int CurrentGameId;

        public override string ToString() {
            return UserName + " (" + ID + ")";
        }

        public int GetScore(bool isHardcore) {
            return 0; // todo
        }

        public UserGameData RegisterGame(GameData game) {
            UserGameData ugd = new UserGameData() {
                GameID = game.ID,
                Achievements = new Dictionary<int, UserAchievementData>(),
                PresenceHistory = new List<PresenceHistory>(),
                FirstPlay = DateTime.Now,
                LastPlay = DateTime.Now,
                PlayTimeLastPing = DateTime.Now
            };
            GameData.Add(game.ID, ugd);
            return ugd;
        }
    }
}
