// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

using Newtonsoft.Json;

namespace LAHEE.Data.File;

public class GameDataJsonV1 {
    public uint ID;
    public string Title;
    public string ImageIcon;
    public string RichPresencePatch;
    public int ConsoleID;
    public string ImageIconURL;
    public List<AchievementData> Achievements;
    public List<LeaderboardData> Leaderboards;
    public List<string> ROMHashes = new List<string>();
    [JsonIgnore] public string SourceFilePath;

    public GameDataJsonV1() {
    }

    public GameDataJsonV1(GameData data) {
        ID = data.ID;
        Title = data.Title;
        ImageIcon = data.ImageIcon;
        RichPresencePatch = data.RichPresencePatch;
        ConsoleID = data.ConsoleID;
        ImageIconURL = data.ImageIconURL;
        Achievements = data.GetAllAchievements().ToList();
        Leaderboards = data.GetAllLeaderboards().ToList();
        ROMHashes = data.ROMHashes;
    }
}