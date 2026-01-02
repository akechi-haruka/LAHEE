// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

public class LeaderboardData {
    public int ID;
    public String Mem;
    public String Format;
    public int LowerIsBetter;
    public String Title;
    public String Description;
    public bool Hidden;

    public override string ToString() {
        return Title + " (" + ID + ")";
    }
}