using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE.Data {
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
                FirstPlay = DateTime.Now,
                PlayTimeLastPing = DateTime.Now
            };
            GameData.Add(game.ID, ugd);
            return ugd;
        }
    }
}
