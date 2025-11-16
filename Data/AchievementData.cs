// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

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
    public int Flags;
    public String Type;
    public float Rarity;
    public float RarityHardcore;
    public String BadgeURL;
    public String BadgeLockedURL;

    internal static string ConvertType(string type) {
        switch (type) {
            case "1": return "missable";
            case "2": return "progression";
            case "3": return "win_condition";
            case "": return "";
            default: throw new ArgumentException("unknown achievement type: " + type);
        }
    }

    public override string ToString() {
        return Title + " (" + ID + ")";
    }
}