// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

public class UserData {
    public int ID;
    public bool AllowUse;
    public String UserName;
    public Dictionary<uint, UserGameData> GameData;
    public uint CurrentGameId;

    public override string ToString() {
        return UserName + " (" + ID + ")";
    }

    public int GetScore(bool isHardcore) {
        return (from ugd in GameData.Values from uad in ugd.Achievements.Values where uad.Status == UserAchievementData.StatusFlag.HardcoreUnlock && isHardcore || uad.Status == UserAchievementData.StatusFlag.SoftcoreUnlock && !isHardcore select StaticDataManager.FindGameDataById(ugd.GameID)?.GetAchievementById(uad.AchievementID)?.Points ?? 0).Sum();
    }

    public UserGameData RegisterGame(GameData game) {
        UserGameData ugd = new UserGameData() {
            GameID = game.ID,
            Achievements = new Dictionary<int, UserAchievementData>(),
            PresenceHistory = new List<PresenceHistory>(),
            FirstPlay = DateTime.Now,
            LastPlay = DateTime.Now,
            PlayTimeLastPing = DateTime.Now,
            FlaggedAchievements = new List<int>()
        };
        GameData.Add(game.ID, ugd);
        return ugd;
    }
}