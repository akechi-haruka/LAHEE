using LAHEE.Data;
using LAHEE.Data.File;
using RATools.Data;
using CodeNote = LAHEE.Data.CodeNote;

// these are defined by RA, therefore disable checks

// ReSharper disable All
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace LAHEE;

class RAAnyResponse {
    public bool Success;
}

class RAErrorResponse : RAAnyResponse {
    public string Error;
    public int Code;

    public RAErrorResponse(string error) {
        Error = error;
    }
}

class RALoginResponse : RAAnyResponse {
    public string User;
    public string Token;
    public int Score;
    public int SoftcoreScore;
    public int Messages;
    public int Permissions;
    public string AccountType;
    public string DisplayName;
}

class RAGameIDResponse : RAAnyResponse {
    public uint GameID;
}

class RAPatchResponse : RAAnyResponse {
    public GameData PatchData;
}

class RAPatchResponseV2 : RAAnyResponse {
    public uint GameId;
    public String Title;
    public String ImageIconUrl;
    public uint RichPresenceGameId;
    public String RichPresencePatch;
    public int ConsoleId;
    public List<SetData> Sets;
}

class RAStartSessionResponse : RAAnyResponse {
    public RAStartSessionAchievementData[] Unlocks;
    public RAStartSessionAchievementData[] HardcoreUnlocks;
    public long ServerNow;

    public class RAStartSessionAchievementData {
        public int ID;
        public long When;

        public RAStartSessionAchievementData() {
        }

        public RAStartSessionAchievementData(UserAchievementData userAchievement, bool isHardcore) {
            ID = userAchievement.AchievementID;
            When = isHardcore ? userAchievement.AchieveDate : userAchievement.AchieveDateSoftcore;
        }
    }
}

class RAAchievementListResponse : RAAnyResponse {
    public int[] UserUnlocks;
    public int GameId;
    public bool HardcoreMode;
}

class RAUnlockResponse : RAAnyResponse {
    public int Score;
    public int SoftcoreScore;
    public int AchievementID;
    public int AchievementsRemaining;
}

class RALeaderboardResponse : RAAnyResponse {
    public RALeaderboardResponseV2 Response;
    public int Score;
    public int BestScore;
    public RankObject RankInfo;
    public TopObject[] TopEntries;

    public class RankObject {
        public int Rank;
        public int NumEntries;
    }

    public class TopObject {
        public String User;
        public int Rank;
        public int Score;
    }
}

class RALeaderboardResponseV2 : RAAnyResponse {
    public LeaderboardData LBData;
    public int Score;
    public String ScoreFormatted;
    public int BestScore;
    public RALeaderboardResponse.RankObject RankInfo;
    public RALeaderboardResponse.TopObject[] TopEntries;
    public RALeaderboardResponse.TopObject[] TopEntriesFriends;
}

class LaheeResponse {
    public String version;
    public UserData[] users;
    public GameDataJsonV1[] games;
    public UserComment[] comments;
}

class LaheeUserResponse {
    public uint currentgameid;
    public DateTime? lastping;
    public DateTime? lastplay;
    public TimeSpan? playtime;
    public String gamestatus;
    public Dictionary<int, UserAchievementData> achievements;
}

class RAApiHashesResponse {
    public Hash[] Results;

    internal class Hash {
        public String MD5;
        public String Name;
        public String[] Labels;
        public String PatchUrl;
    }
}

class RAApiCommentsResponse {
    public int Count;
    public int Total;
    public UserComment[] Results;
}

class LaheeFetchCommentsResponse : RAAnyResponse {
    public UserComment[] Comments;
}

class LaheeWriteCommentResponse : LaheeFetchCommentsResponse {
}

class LaheeFlagImportantResponse : RAAnyResponse {
    public List<int> Flagged;
}

class RALatestIntegrationResponse : RAAnyResponse {
    public String MinimumVersion;
    public String LatestVersion;
    public String LatestVersionUrl;
    public String LatestVersionUrlX64;
}

class RACodeNotesResponse : RAAnyResponse {
    public List<CodeNote> CodeNotes;
}

class RABadgeIterResponse : RAAnyResponse {
    public int FirstBadge;
    public int NextBadge;
}

class RAUploadAchievementResponse : RAAnyResponse {
    public int AchievementID;
    public String Error;
}

class RAUploadFileResponse : RAAnyResponse {
    public ResponseClass Response;

    public class ResponseClass {
        public String BadgeIter;
    }
}

class RARichPresencePatchResponse : RAAnyResponse {
    public String RichPresencePatch;
}

class LaheeAchievementCodeResponse : RAAnyResponse {
    public List<CodeNote> CodeNotes;
    public RequirementGroup[] TriggerGroups;
}