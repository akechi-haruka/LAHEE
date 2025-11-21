// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

using LAHEE.Data.File;

namespace LAHEE.Data;

public class GameData {
    public const int CURRENT_DATA_VERSION = 3;

    public int DataVersion;
    public uint ID;
    public String Title;
    public String ImageIcon;
    public String RichPresencePatch;
    public int ConsoleID;
    public String ImageIconURL;
    public List<SetData> AchievementSets;
    public List<String> ROMHashes = new List<string>();
    public List<CodeNote> CodeNotes = new List<CodeNote>();

    public GameData() {
    }

    public GameData(GameDataJsonV1 legacy) {
        DataVersion = 1;
        ID = legacy.ID;
        Title = legacy.Title;
        ImageIcon = legacy.ImageIcon;
        RichPresencePatch = legacy.RichPresencePatch;
        ConsoleID = legacy.ConsoleID;
        ImageIconURL = legacy.ImageIconURL;
        ROMHashes = legacy.ROMHashes;
        AchievementSets = new List<SetData>();
        AchievementSets.Add(new SetData() {
            Title = legacy.Title,
            AchievementSetId = 1,
            GameId = StaticDataManager.RAIntegrationAssertionWorkaround(legacy.ID),
            ImageIconUrl = legacy.ImageIconURL,
            Type = SetType.core,
            FileSource = null,
            Achievements = legacy.Achievements ?? new List<AchievementData>(),
            Leaderboards = legacy.Leaderboards ?? new List<LeaderboardData>()
        });
        Upgrade();
    }

    public override string ToString() {
        return Title + " (" + ID + ")";
    }

    public AchievementData GetAchievementById(int achievementId) {
        return GetAllAchievements().Where(a => a.ID == achievementId).FirstOrDefault((AchievementData)null);
    }

    public LeaderboardData GetLeaderboardById(int leaderboardId) {
        return GetAllLeaderboards().Where(l => l.ID == leaderboardId).FirstOrDefault((LeaderboardData)null);
    }

    public AchievementData GetAchievementByName(string str, bool partial) {
        return GetAllAchievements().FirstOrDefault(r => partial ? r.Title.Contains(str) : r.Title.Equals(str));
    }

    public void Upgrade() {
        if (DataVersion == CURRENT_DATA_VERSION) {
            return;
        }

        if (CodeNotes == null) {
            CodeNotes = new List<CodeNote>();
        }

        if (AchievementSets == null) {
            AchievementSets = new List<SetData>();
        }

        DataVersion = CURRENT_DATA_VERSION;
    }

    public int GetAchievementCount() {
        return GetAllAchievements().Count();
    }

    public IEnumerable<AchievementData> GetAllAchievements() {
        return AchievementSets.SelectMany(set => set.Achievements);
    }

    public IEnumerable<LeaderboardData> GetAllLeaderboards() {
        return AchievementSets.SelectMany(set => set.Leaderboards);
    }

    public void DeleteAchievementById(int id) {
        AchievementSets.ForEach(set => set.Achievements.RemoveAll(a => a.ID == id));
    }

    public SetData GetCoreAchievementSet() {
        return AchievementSets.Find(set => set.Type == SetType.core);
    }
}