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

public enum AchievementType {
    missable,
    progression,
    win_condition
}

[Flags]
public enum AchievementFlags {
    Always = 1,
    Official = 2,
    Unofficial = 4
}

public class AchievementData {
    public int ID;
    public String MemAddr;
    public String Title;
    public String Description;
    public int Points;
    public String Author;
    public long Modified;
    public long Created;
    public String BadgeName;
    public AchievementFlags Flags;

    [JsonConverter(typeof(StringEnumConverter))]
    public AchievementType? Type;

    public float Rarity;
    public float RarityHardcore;
    public String BadgeURL;
    public String BadgeLockedURL;

    internal static AchievementType? ConvertType(string type) {
        switch (type) {
            case "1": return AchievementType.missable;
            case "2": return AchievementType.progression;
            case "3": return AchievementType.win_condition;
            case "": return null;
            default: throw new ArgumentException("unknown achievement type: " + type);
        }
    }

    public override string ToString() {
        return Title + " (" + ID + ")";
    }
}