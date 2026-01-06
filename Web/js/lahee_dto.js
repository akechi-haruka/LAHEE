// noinspection JSUnusedGlobalSymbols

/* Data structure */

const LaheeAchievementType = Object.freeze({
    missable: "missable",
    progression: "progression",
    win_condition: "win_condition"
});

const LaheeAchievementFlags = Object.freeze({
    Always: 1,
    Official: 2,
    Unofficial: 4
});

class LaheeAchievementData {
    /** @Type {LaheeSetData} */
    Set;
    /** @type {number} */
    ID;
    /** @type {string} */
    MemAddr;
    /** @type {string} */
    Title;
    /** @type {string} */
    Description;
    /** @type {number} */
    Points;
    /** @type {string} */
    Author;
    /** @type {Date} */
    Modified;
    /** @type {Date} */
    Created;
    /** @type {string} */
    BadgeName;
    /** @type {number} */
    Flags;
    /** @type {LaheeAchievementType|null} */
    Type;
    /** @type {float} */
    Rarity;
    /** @type {float} */
    RarityHardcore;
    /** @type {string} */
    BadgeURL;
    /** @type {string} */
    BadgeLockedURL;

    constructor(set_data, data) {
        Object.assign(this, data);
        this.Set = set_data;
        this.Modified = new Date(data.Modified * 1000);
        this.Created = new Date(data.Created * 1000);
        this.Type = LaheeAchievementType[data.Type];
    }

    /** @type {LaheeGameData} */
    getGameData() {
        return this.Set.Game;
    }

    toString() {
        return this.Title + " (" + this.ID + ")";
    }
}

class LaheeCodeNote {
    /** @type {string} */
    User;
    /** @type {string} */
    Address;
    /** @type {string} */
    Note;

    constructor(data) {
        Object.assign(this, data);
    }
}

class LaheeGameData {
    /** @type {number} */
    DataVersion;
    /** @type {number} */
    ID;
    /** @type {string} */
    Title;
    /** @type {string} */
    ImageIcon;
    /** @type {string} */
    RichPresencePatch;
    /** @type {number} */
    ConsoleID;
    /** @type {number} */
    ImageIconURL;
    /** @type {Array.<LaheeSetData>} */
    AchievementSets;
    /** @type {Array.<string>} */
    ROMHashes;
    /** @type {Array.<LaheeCodeNote>} */
    CodeNotes;

    constructor(data) {
        Object.assign(this, data);
        this.AchievementSets = this.AchievementSets.map(s => new LaheeSetData(this, s));
        this.CodeNotes = this.CodeNotes.map(cn => new LaheeCodeNote(this, cn));
    }

    /**
     * @type {?LaheeAchievementData}
     * @param {number} id
     */
    getAchievementById(id) {
        return this.getAllAchievements().find(a => a.ID == id);
    }

    /** @type {Array.<LaheeAchievementData>} */
    getAllAchievements() {
        return this.AchievementSets.flatMap(as => as.Achievements);
    }

    /** @type {Array.<LaheeLeaderboardData>} */
    getAllLeaderboards() {
        return this.AchievementSets.flatMap(as => as.Leaderboards);
    }

    /**
     * @type {?LaheeUserGameData}
     * @param {LaheeUserData} user
     */
    getUserData(user) {
        return user?.GameData[this.ID];
    }


    /**
     * @type {?LaheeLeaderboardData}
     * @param {number} leaderboard_id
     */
    getLeaderboard(leaderboard_id) {
        return this.getAllLeaderboards().find(lb => lb.ID == leaderboard_id);
    }

    toString() {
        return this.Title + " (" + this.ID + ")";
    }
}

class LaheeLeaderboardData {
    /** @type {LaheeSetData} */
    Set;
    /** @type {number} */
    ID;
    /** @type {string} */
    Mem;
    /** @type {string} */
    Format;
    /** @type {number} */
    LowerIsBetter;
    /** @type {string} */
    Title;
    /** @type {string} */
    Description;
    /** @type {boolean} */
    Hidden;

    constructor(set_data, data) {
        Object.assign(this, data);
        this.SetData = set_data;
    }

    toString() {
        return this.Title + " (" + this.ID + ")";
    }
}

class LaheePresenceHistory {
    /** @type {Date} */
    Time;
    /** @type {string} */
    Message;

    constructor(data) {
        this.Time = new Date(data.Time * 1000);
        this.Message = data.Message;
    }
}

const LaheeSetType = Object.freeze({
    core: "core",
    bonus: "bonus",
    specialty: "specialty",
    exclusive: "exclusive"
});

class LaheeSetData {
    /** @type {LaheeGameData} */
    Game;
    /** @type {string} */
    Title;
    /** @type {string} */
    Type;
    /** @type {number} */
    AchievementSetId;
    /** @type {number} */
    GameId;
    /** @type {string} */
    ImageIconUrl;
    /** @type {Array.<LaheeAchievementData>} */
    Achievements;
    /** @type {Array.<LaheeLeaderboardData>} */
    Leaderboards;

    constructor(game_data, data) {
        Object.assign(this, data);
        this.Game = game_data;
        this.Achievements = data.Achievements.map(a => new LaheeAchievementData(this, a));
        this.Leaderboards = data.Leaderboards.map(l => new LaheeLeaderboardData(this, l));
    }
}

const LaheeUserAchievementStatus = Object.freeze({
    Locked: 0,
    SoftcoreUnlock: 1,
    HardcoreUnlock: 2
});

class LaheeUserAchievementData {
    /** @type {LaheeUserGameData} */
    UserGame;
    /** @type {number} */
    AchievementID;
    /** @type {number} */
    Status;
    /** @type {Date|null} */
    AchieveDate
    /** @type {Date|null} */
    AchieveDateSoftcore;
    /** @type {TimeSpan|null} */
    AchievePlaytime;
    /** @type {TimeSpan|null} */
    AchievePlaytimeSoftcore;

    constructor(user_game_data, data) {
        Object.assign(this, data);
        this.UserGame = user_game_data;
        if (data) {
            this.AchieveDate = data.AchieveDate ? new Date(data.AchieveDate * 1000) : null;
            this.AchieveDateSoftcore = data.AchieveDateSoftcore ? new Date(data.AchieveDateSoftcore * 1000) : null;
            this.AchievePlaytime = data.AchievePlaytime ? TimeSpan.parse(data.AchievePlaytime) : null;
            this.AchievePlaytimeSoftcore = data.AchievePlaytimeSoftcore ? TimeSpan.parse(data.AchievePlaytimeSoftcore) : null;
        } else {
            this.Status = LaheeUserAchievementStatus.Locked;
        }
    }

    /** @type {?LaheeAchievementData} */
    getAchievementData() {
        return this.UserGame.getGameData()?.getAchievementById(this.AchievementID);
    }

    /** @type {?Date} */
    getLaterAchieveDate() {
        if (!this.AchieveDate) {
            return this.AchieveDateSoftcore;
        }
        return this.AchieveDate > this.AchieveDateSoftcore ? this.AchieveDate : this.AchieveDateSoftcore;
    }

    /** @type {?TimeSpan} */
    getLaterPlaytime() {
        if (!this.AchieveDate) {
            return this.AchievePlaytimeSoftcore;
        }
        return this.AchieveDate > this.AchieveDateSoftcore ? this.AchievePlaytime : this.AchievePlaytimeSoftcore;
    }

    /** @type {?Date} */
    getAchieveDate() {
        switch (this.Status) {
            case LaheeUserAchievementStatus.SoftcoreUnlock:
                return this.AchieveDateSoftcore;
            case LaheeUserAchievementStatus.HardcoreUnlock:
                return this.AchieveDate;
            default:
                return null;
        }
    }

    /** @type {?TimeSpan} */
    getAchievePlaytime() {
        switch (this.Status) {
            case LaheeUserAchievementStatus.SoftcoreUnlock:
                return this.AchievePlaytimeSoftcore ?? TimeSpan.zero;
            case LaheeUserAchievementStatus.HardcoreUnlock:
                return this.AchievePlaytime ?? TimeSpan.zero;
            default:
                return TimeSpan.zero;
        }
    }

    toString() {
        return this.AchievementID;
    }
}

class LaheeUserComment {
    /** @type {number} */
    AchievementID;
    /** @type {string} */
    ULID;
    /** @type {string} */
    User;
    /** @type {Date} */
    Submitted;
    /** @type {string} */
    CommentText;
    /** @type {boolean} */
    IsLocal;
    /** @type {string} */
    LaheeUUID;

    constructor(data) {
        Object.assign(this, data);
        this.Submitted = new Date(data.Submitted * 1000);
    }
}

class LaheeUserData {
    /** @type {number} */
    ID;
    /** @type {boolean} */
    AllowUse;
    /** @type {string} */
    UserName;
    /** @type {Object.<string, LaheeUserGameData>} */
    GameData;
    /** @type {number} */
    CurrentGameId;

    constructor(data) {
        Object.assign(this, data);
        this.GameData = {};
        Object.values(data.GameData).forEach(ugd => this.GameData[ugd.GameID] = new LaheeUserGameData(this, ugd));
    }

    /**
     * @type {Array.<string>}
     */
    getAllGameIds() {
        return Object.keys(this.GameData);
    }

    /**
     * @type {Array.<LaheeUserGameData>}
     */
    getAllGameData() {
        return Object.values(this.GameData);
    }

    /**
     * @type {Array.<LaheeUserAchievementData>}
     */
    getAllAchievements() {
        return this.getAllGameData().flatMap(ugd => Object.values(ugd.Achievements));
    }

    /**
     * @type {Array.<LaheeUserAchievementData>}
     */
    getAllUnlockedAchievements() {
        return this.getAllAchievements().filter(ua => ua.Status != LaheeUserAchievementStatus.Locked);
    }

    /**
     * @type {?LaheeUserAchievementData}
     * @param achievement_id {LaheeAchievementData|number}
     */
    getAchievementData(achievement_id) {
        return this.getAllGameData().flatMap(ugd => Object.values(ugd.Achievements)).find(ua => ua.AchievementID == (achievement_id.ID ?? achievement_id));
    }

    /**
     * @type {LaheeUserGameData}
     * @param game {LaheeGameData}
     */
    getUserGameData(game) {
        return this.GameData[game.ID] ?? new LaheeUserGameData(this);
    }

    /**
     * @type {Array.<LaheeUserLeaderboardData>}
     * @param leaderboard_id {number}
     */
    getLeaderboardScoresFor(leaderboard_id) {
        for (var ug of this.getAllGameData()) {
            if (ug.LeaderboardEntries[leaderboard_id]) {
                return ug.LeaderboardEntries[leaderboard_id];
            }
        }
        return [];
    }
}

class LaheeUserGameData {
    /** @type {LaheeUserData} */
    User;
    /** @type {number} */
    GameID;
    /** @type {string} */
    LastPresence;
    /** @type {Object.<string, LaheeUserAchievementData>} */
    Achievements;
    /** @type {Object.<string, Array.<LaheeUserLeaderboardData>>} */
    LeaderboardEntries;
    /** @type {Array.<PresenceHistory>} */
    PresenceHistory;
    /** @type {Array.<number>} */
    FlaggedAchievements;
    /** @type {Date} */
    FirstPlay;
    /** @type {Date} */
    LastPlay;
    /** @type {TimeSpan} */
    PlayTimeApprox;

    constructor(user_data, data) {
        Object.assign(this, data);
        this.User = user_data;
        this.Achievements = {};
        this.LeaderboardEntries = {};
        if (data) {
            if (data.Achievements) {
                Object.values(data.Achievements).forEach(ua => this.Achievements[ua.AchievementID] = new LaheeUserAchievementData(this, ua));
            }
            if (data.LeaderboardEntries) {
                Object.keys(data.LeaderboardEntries).forEach(lid => this.LeaderboardEntries[lid] = data.LeaderboardEntries[lid].map(ule => new LaheeUserLeaderboardData(this, ule)));
            }
            this.FirstPlay = new Date(data.FirstPlay);
            this.LastPlay = new Date(data.LastPlay);
            this.PlayTimeApprox = TimeSpan.parse(data.PlayTimeApprox);
        } else {
            this.PresenceHistory = [];
            this.FlaggedAchievements = [];
            this.PlayTimeApprox = TimeSpan.zero;
        }
    }

    /** @type {?LaheeGameData} */
    getGameData() {
        return lahee_data.getGameById(this.GameID);
    }

    /** @type {Array.<LaheeUserAchievementData>} */
    getAllAchievements() {
        return Object.values(this.Achievements);
    }

    /** @type {?LaheeUserAchievementData}
     * @param {LaheeAchievementData|number} achievement_id
     */
    getAchievementData(achievement_id) {
        return this.Achievements[achievement_id?.ID ?? achievement_id] ?? new LaheeUserAchievementData(this, null);
    }

}

class LaheeUserLeaderboardData {
    /** @type {LaheeUserGameData} */
    UserGame;
    /** @type {number} */
    LeaderboardID;
    /** @type {number} */
    Score;
    /** @type {Date} */
    RecordDate;
    /** @type {TimeSpan} */
    PlayTime;
    /** @type {number} */
    _presort_index;

    constructor(user_game_data, data) {
        Object.assign(this, data);
        this.UserGameData = user_game_data;
        this.RecordDate = new Date(data.RecordDate * 1000);
        this.PlayTime = TimeSpan.parse(data.PlayTime);
    }
}

/* Network responses */

class LaheeInfoResponse {
    /** @type {string} */
    version;
    /** @type {Array.<LaheeUserData>} */
    users;
    /** @type {Array.<LaheeGameData>} */
    games;
    /** @type {Array.<LaheeUserComment>} */
    comments;

    constructor(data) {
        this.version = data.version;
        this.users = data.users.map(u => new LaheeUserData(u));
        this.games = data.games.map(g => new LaheeGameData(g));
        this.comments = data.comments.map(c => new LaheeUserComment(c));
    }

    /**
     * @type {?LaheeUserData}
     * @param id {number}
     */
    getUserById(id) {
        return this.users.find(u => u.ID == id);
    }

    /**
     * @type {?LaheeGameData}
     * @param id {number}
     */
    getGameById(id) {
        return this.games.find(g => g.ID == id);
    }

    /**
     * @type {?LaheeUserComment}
     * @param id {string}
     */
    getCommentByLaheeUUID(id) {
        return this.comments.find(c => c.LaheeUUID == id);
    }

    /**
     * @type {?LaheeAchievementData}
     * @param id {number}
     */
    getAchievementById(id) {
        return this.getAllAchievements().find(a => a.ID == id);
    }

    /**
     * @type {Array.<LaheeAchievementData>}
     */
    getAllAchievements() {
        return this.games.flatMap(g => g.getAllAchievements());
    }

}

class LaheeUserResponse {
    /** @type {number} */
    current_game_id;
    /** @type {Date|null} */
    last_ping;
    /** @type {Date|null} */
    last_play;
    /** @type {TimeSpan|null} */
    play_time;
    /** @type {string} */
    game_status;
    /** @type {Object.<string, LaheeUserAchievementData>} */
    achievements;

    constructor(user_game_data, data) {
        this.current_game_id = data.current_game_id;
        this.last_ping = data.last_ping ? new Date(data.last_ping) : null;
        this.last_play = data.last_play ? new Date(data.last_play) : null;
        this.play_time = data.play_time ? TimeSpan.parse(data.play_time) : null;
        this.game_status = data.game_status;
        this.achievements = {};
        Object.values(data.achievements).forEach(ua => this.achievements[ua.AchievementID] = new LaheeUserAchievementData(user_game_data, ua));
    }
}

class RAResponseBase {
    /** @type {boolean} */
    Success;
    /** @type {number} */
    Code;
    /** @type {string} */
    Error;

    constructor(data) {
        Object.assign(this, data);
    }
}

class LaheeFetchCommentsResponse extends RAResponseBase {
    /** @type {Array.<LaheeUserComment>} */
    Comments;

    constructor(data) {
        super(data);
        this.Comments = data.Comments?.map(c => new LaheeUserComment(c));
    }
}

class LaheeFlagAchievementResponse extends RAResponseBase {
    /** @type {Array.<number>} */
    Flagged;

    constructor(data) {
        super(data);
        this.Flagged = data.Flagged;
    }
}

class LaheeAchievementCodeResponse extends RAResponseBase {
    /** @type {Array.<LaheeCodeNote>} */
    CodeNotes;
    /** @type {Array.<RAToolsRequirementGroup>} */
    TriggerGroups;

    constructor(data) {
        super(data);
        this.CodeNotes = data.CodeNotes.map(cn => new LaheeCodeNote(cn));
        this.TriggerGroups = data.TriggerGroups.map(g => new RAToolsRequirementGroup(g));
    }
}

class LaheeLiveTickerEvent {
    /** @type {string} */
    type;

    constructor(data) {
        this.type = data.type;
    }
}

const LaheePingType = Object.freeze({
    Time: "Time",
    AchievementUnlock: "AchievementUnlock",
    LeaderboardRecorded: "LeaderboardRecorded"
});

class LaheeLivePingEvent {
    /** @type {LaheePingType} */
    pingType;

    constructor(data) {
        this.pingType = data.pingType;
    }
}

class LaheeLiveUnlockEvent {
    /** @type {number} */
    gameId;
    /** @type {LaheeUserAchievementData} */
    userAchievementData;

    constructor(data) {
        this.gameId = data.gameId;
        this.userAchievementData = new LaheeUserAchievementData(null, data.userAchievementData);
    }
}

class RAToolsRequirementGroup {
    /** @type {Array.<RAToolsRequirement>} */
    Requirements;

    constructor(data) {
        this.Requirements = data.Requirements.map(r => new RAToolsRequirement(r));
    }
}

class RAToolsRequirement {
    /** @type {RAToolsField} */
    Left;
    /** @type {RAToolsField} */
    Right;
    /** @type {number} */
    Type;
    /** @type {boolean} */
    IsComparison;
    /** @type {boolean} */
    IsMeasured;
    /** @type {number} */
    Operator;
    /** @type {number} */
    HitCount;

    constructor(data) {
        Object.assign(this, data);
        this.Left = new RAToolsField(data.Left);
        this.Right = new RAToolsField(data.Right);
    }

    /** @returns {string} */
    getTriggerTypeName() {
        switch (this.Type) {
            case 0:
                return "";
            case 1:
                return "ResetIf";
            case 2:
                return "PauseIf";
            case 3:
                return "AddSource";
            case 4:
                return "SubSource";
            case 5:
                return "AddHits";
            case 6:
                return "SubHits";
            case 7:
                return "AndNext";
            case 8:
                return "OrNext";
            case 9:
                return "Measured";
            case 10:
                return "MeasuredIf";
            case 11:
                return "AddAddress";
            case 12:
                return "ResetNextIf";
            case 13:
                return "Trigger";
            case 14:
                return "MeasuredPercent";
            case 15:
                return "Remember";
            default:
                return "Unknown: " + this.Type;
        }
    }

    /** @returns {string} */
    getTriggerTypeDescription() {
        switch (this.Type) {
            case 0:
                return "";
            case 1:
                return "Resets any HitCounts in the current requirement group if true.";
            case 2:
                return "Pauses processing of the achievement if true.";
            case 3:
                return "Adds the Left part of the requirement to the Left part of the next requirement.";
            case 4:
                return "Subtracts the Left part of the next requirement from the Left part of the requirement.";
            case 5:
                return "Adds the HitsCounts from this requirement to the next requirement.";
            case 6:
                return "Subtracts the HitsCounts from this requirement from the next requirement.";
            case 7:
                return "This requirement must also be true for the next requirement to be true.";
            case 8:
                return "This requirement or the following requirement must be true for the next requirement to be true.";
            case 9:
                return "Meta-flag indicating that this condition tracks progress as a raw value.";
            case 10:
                return "Meta-flag indicating that this condition must be true to track progress.";
            case 11:
                return "Adds the Left part of the requirement to the addresses in the next requirement.";
            case 12:
                return "Resets any HitCounts on the next requirement group if true.";
            case 13:
                return "While all non-Trigger conditions are true, a challenge indicator will be displayed.";
            case 14:
                return "Meta-flag indicating that this condition tracks progress as a percentage.";
            case 15:
                return "Meta-flag to capture the accumulator for further modification.";
            default:
                return "Unknown: " + this.Type;
        }
    }

    /** @returns {string} */
    getOperatorText() {
        switch (this.Operator) {
            case 0:
                return "";
            case 1:
                return "==";
            case 2:
                return "!=";
            case 3:
                return "<";
            case 4:
                return "<=";
            case 5:
                return ">";
            case 6:
                return ">=";
            case 7:
                return "+";
            case 8:
                return "-";
            case 9:
                return "*";
            case 10:
                return "/";
            case 11:
                return "&";
            case 12:
                return "^";
            case 13:
                return "%";
            default:
                return "Unknown: " + this.Operator;
        }
    }
}

class RAToolsField {
    /** @type {number} */
    Type;
    /** @type {number} */
    Size;
    /** @type {number} */
    Value;
    /** @type {Float} */
    Float;
    /** @type {boolean} */
    IsMemoryReference;
    /** @type {boolean} */
    IsFloat;
    /** @type {boolean} */
    IsBigEndian;

    constructor(data) {
        Object.assign(this, data);
    }

    /** @returns {string} */
    getFieldTypeName() {
        switch (this.Type) {
            case 0:
                return "";
            case 1:
                return "Memory";
            case 2:
                return "Constant";
            case 3:
                return "Delta";
            case 4:
                return "Prior";
            case 5:
                return "Memory (BCD)";
            case 6:
                return "Float";
            case 7:
                return "Memory (Inverse)";
            case 8:
                return "Recall";
            default:
                return "Unknown: " + this.Type;
        }
    }

    /** @returns {string} */
    getFieldTypeDescription() {
        switch (this.Type) {
            case 0:
                return "";
            case 1:
                return "The value at a memory address.";
            case 2:
                return "An unsigned integer constant.";
            case 3:
                return "The previous value at a memory address.";
            case 4:
                return "The last differing value at a memory address.";
            case 5:
                return "The current value at a memory address decoded from BCD.";
            case 6:
                return "A floating point constant.";
            case 7:
                return "The bitwise inversion of the value at a memory address.";
            case 8:
                return "The accumulator captured by a Remember condition.";
            default:
                return "Unknown: " + this.Type;
        }
    }

    /** @returns {string} */
    getSizeName() {
        switch (this.Size) {
            case 0:
                return "";
            case 1:
                return "Bit 0";
            case 2:
                return "Bit 1";
            case 3:
                return "Bit 2";
            case 4:
                return "Bit 3";
            case 5:
                return "Bit 4";
            case 6:
                return "Bit 5";
            case 7:
                return "Bit 6";
            case 8:
                return "Bit 7";
            case 9:
                return "Low Nibble";
            case 10:
                return "High Nibble";
            case 11:
                return "8-bit";
            case 12:
                return "16-bit LE";
            case 13:
                return "24-bit LE";
            case 14:
                return "32-bit LE";
            case 15:
                return "BitCount";
            case 16:
                return "16-bit BE";
            case 17:
                return "24-bit BE";
            case 18:
                return "32-bit BE";
            case 19:
                return "Float";
            case 20:
                return "MBF32";
            case 21:
                return "MBF32 LE";
            case 22:
                return "Float BE";
            case 23:
                return "Double";
            case 24:
                return "Double BE";
            case 25:
                return "Array";
            default:
                return "Unknown: " + this.Size;
        }
    }

    /** @returns {string} */
    getSizeDescription() {
        switch (this.Size) {
            case 0:
                return "";
            case 1:
                return "Bit 0 of a byte.";
            case 2:
                return "Bit 1 of a byte.";
            case 3:
                return "Bit 2 of a byte.";
            case 4:
                return "Bit 3 of a byte.";
            case 5:
                return "Bit 4 of a byte.";
            case 6:
                return "Bit 5 of a byte.";
            case 7:
                return "Bit 6 of a byte.";
            case 8:
                return "Bit 7 of a byte.";
            case 9:
                return "Bits 0-3 of a byte.";
            case 10:
                return "Bits 4-7 of a byte.";
            case 11:
                return "A byte (8-bits).";
            case 12:
                return "Two bytes (16-bit). Read from memory in little-endian mode.";
            case 13:
                return "Three bytes (24-bit). Read from memory in little-endian mode.";
            case 14:
                return "Four bytes (32-bit). Read from memory in little-endian mode.";
            case 15:
                return "The number of bits set in a byte.";
            case 16:
                return "Two bytes (16-bit). Read from memory in big-endian mode.";
            case 17:
                return "Three bytes (24-bit). Read from memory in big-endian mode.";
            case 18:
                return "Four bytes (32-bit). Read from memory in big-endian mode.";
            case 19:
                return "32-bit IEE-754 floating point number.";
            case 20:
                return "32-bit Microsoft Binary Format floating point number.";
            case 21:
                return "32-bit Microsoft Binary Format floating point number in little-endian mode.";
            case 22:
                return "32-bit IEE-754 floating point number in big-endian mode";
            case 23:
                return "Most significant 32-bits of an IEE-754 double number (64-bit float).";
            case 24:
                return "Most significant 32-bits of an IEE-754 double number (64-bit float) in big endian mode.";
            case 25:
                return "Virtual size indicating a value takes an arbitrary number of bytes";
            default:
                return "Unknown: " + this.Size;
        }
    }

}