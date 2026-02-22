using RATools.Data;

namespace LAHEE.Data;

public class AchievementExtendedData {
    public int ID;
    public bool IsTrigger;
    public uint MeasuredMax;

    public AchievementExtendedData(AchievementData a) {
        ID = a.ID;
        Trigger trigger = Trigger.Deserialize(a.MemAddr);
        if (trigger == null) {
            throw new NullReferenceException("Trigger deserialization failed for achievement " + a.ID);
        }

        IsTrigger = trigger.Groups.SelectMany(g => g.Requirements).Any(r => r.Type == RequirementType.Trigger);
        Requirement measured = trigger.Groups.SelectMany(g => g.Requirements).FirstOrDefault(r => r.Type is RequirementType.Measured or RequirementType.MeasuredPercent);
        if (measured != null) {
            MeasuredMax = measured.Right.IsMemoryReference || (measured.Left.Type == FieldType.Value && measured.Right.Type == FieldType.Value && measured.Left.Value != measured.Right.Value) ? measured.HitCount : measured.Right.Value;
        }
    }
}