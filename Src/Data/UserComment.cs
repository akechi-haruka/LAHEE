// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LAHEE.Data;

public class UserComment {
    public int AchievementID;
    public String ULID;
    public String User;
    public DateTime Submitted;
    public String CommentText;
    public bool IsLocal;
    public Guid LaheeUUID;
}