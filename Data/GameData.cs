// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

public class GameData {
    private const int CURRENT_DATA_VERSION = 2;

    public int DataVersion;
    public int ID;
    public String Title;
    public String ImageIcon;
    public String RichPresencePatch;
    public int ConsoleID;
    public String ImageIconURL;
    public AchievementData[] Achievements;
    public LeaderboardData[] Leaderboards;
    public List<String> ROMHashes = new List<string>();
    public List<CodeNote> CodeNotes = new List<CodeNote>();

    public override string ToString() {
        return Title + " (" + ID + ")";
    }

    public AchievementData GetAchievementById(int achievementid) {
        return Achievements.Where(a => a.ID == achievementid).FirstOrDefault((AchievementData)null);
    }

    public LeaderboardData GetLeaderboardById(int leaderboardId) {
        return Leaderboards.Where(l => l.ID == leaderboardId).FirstOrDefault((LeaderboardData)null);
    }

    public AchievementData GetAchievementByName(string str, bool partial) {
        if (partial) {
            return Achievements.FirstOrDefault(r => r.Title.Contains(str));
        } else {
            return Achievements.FirstOrDefault(r => r.Title.Equals(str));
        }
    }

    public void Upgrade() {
        if (DataVersion == CURRENT_DATA_VERSION) {
            return;
        }

        if (CodeNotes == null) {
            CodeNotes = new List<CodeNote>();
        }

        DataVersion = CURRENT_DATA_VERSION;
    }
}