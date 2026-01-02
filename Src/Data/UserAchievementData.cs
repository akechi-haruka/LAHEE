// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

public class UserAchievementData {
    public int AchievementID;
    public StatusFlag Status;
    public long AchieveDate;
    public long AchieveDateSoftcore;
    public TimeSpan AchievePlaytime;
    public TimeSpan AchievePlaytimeSoftcore;

    public enum StatusFlag {
        Locked = 0,
        SoftcoreUnlock = 1,
        HardcoreUnlock = 2
    }

    public override string ToString() {
        return AchievementID.ToString();
    }
}