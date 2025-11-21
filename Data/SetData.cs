// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace LAHEE.Data;

public enum SetType {
    core,
    bonus,
    specialty,
    exclusive
}

public class SetData {
    public String Title;

    [JsonConverter(typeof(StringEnumConverter))]
    public SetType Type;

    public int AchievementSetId;
    public uint GameId;
    public String ImageIconUrl;
    public List<AchievementData> Achievements;
    public List<LeaderboardData> Leaderboards;
    [JsonIgnore] public String FileSource;
}