// noinspection EqualityComparisonWithCoercionJS,HtmlRequiredAltAttribute,ExceptionCaughtLocallyJS,JSDuplicatedDeclaration

/** @type {string} */
var LAHEE_URL;
/** @type {LaheeInfoResponse} */
var lahee_data;
/** @type {LaheeUserData} */
var lahee_current_user;
/** @type {LaheeGameData} */
var lahee_current_game;
/** @type {LaheeAchievementData} */
var lahee_current_achievement;
/** @type {number} */
var lahee_last_audio = Date.now();
var lahee_popup;
var lahee_popup_2;
/** @type {Array.<Tooltip>} */
var tooltipList;

function lahee_init() {
    lahee_settings_load();
    lahee_settings_apply();

    if (Notification.permission == "default") {
        Notification.requestPermission().then(p => console.log("Notification permission: " + p));
    }
    lahee_audio_play("540121__jj_om__blank-sound.ogg");

    lahee_split_init_library();

    LAHEE_URL = "http://" + window.location.host + "/dorequest.php";
    lahee_request("r=laheeinfo").then(lahee_init_done).catch(lahee_init_error);
}

function lahee_split_init_library() {
    try {
        var saved_sizes = localStorage.getItem("lahee_setting_split_sizes");
        if (saved_sizes) {
            saved_sizes = JSON.parse(saved_sizes);
        } else {
            saved_sizes = [75, 25];
        }
        // noinspection JSUnresolvedReference
        Split(["#achievement_grid", "#achievement_details"], {
            sizes: saved_sizes,
            onDragEnd: function (sizes) {
                var s = JSON.stringify(sizes);
                localStorage.setItem("lahee_setting_split_sizes", s);
                document.getElementById("lahee_setting_split_sizes").value = s;
            }
        });
    } catch (e) {
        console.error("Error with split.js library", e);
    }
}

/**
 * @param e {string|Error}
 */
function lahee_init_error(e) {
    console.error(e);
    document.getElementById("page_loading").innerHTML = `
        <p>An error has occurred while trying to load data.</p>
        <p><small>${e}</small></p>
        <p><input type="button" value="Retry" class="btn btn-primary" onclick="window.location.reload();" /></p>
    `;
}

/**
 * @param request {string}
 * @returns {Promise<string>}
 */
async function lahee_request(request) {
    console.log("Requesting: " + request);
    return new Promise((resolve, reject) => {
        fetch(LAHEE_URL, {
            body: request,
            method: "POST"
        }).then(resp => {
            if (!resp.ok) {
                return Promise.reject("Network request failed: " + resp.status);
            }
            return resp;
        }).then(resp => {
            return resp.json()
        }).then(data => {
            console.log(data);
            resolve(data)
        }).catch(err => reject(err));
    });
}

/**
 * @param page {string}
 */
function lahee_set_page(page) {
    for (var p of document.getElementsByClassName("lahee-page")) {
        p.style.display = "none";
    }
    for (var p of document.getElementsByClassName("page-controls")) {
        p.style.display = "none";
    }
    document.getElementById(page).style.display = "block";
    for (var c of document.getElementsByClassName(page + "_controls")) {
        c.style.display = "block";
    }
}

/**
 * @param res {string}
 */
function lahee_init_done(res) {

    try {
        lahee_data = new LaheeInfoResponse(res);

        if (lahee_data.users.length == 0) {
            document.getElementById("page_loading").innerHTML = "No user data is registered on LAHEE. Connect your emulator and create user data before attempting to use the web UI.";
            return;
        }
        if (lahee_data.games.length == 0) {
            document.getElementById("page_loading").innerHTML = "No game data is registered on LAHEE. Register games and achievements first before attempting to use the web UI. See readme for more information.";
            return;
        }

        lahee_data.users.sort(function (a, b) {
            return a.UserName.localeCompare(b.UserName);
        });
        lahee_data.games.sort(function (a, b) {
            return a.Title.localeCompare(b.Title);
        });

        document.getElementById("lahee_version").innerText = lahee_data.version;

        var users = "";
        for (var user of lahee_data.users) {
            users += "<option value='" + user.ID + "'>" + user.UserName + "</option>";
        }
        document.getElementById("user_select").innerHTML = users;

        lahee_live_ticker_connect();
        setTimeout(function () {
            try {
                document.getElementById("main_nav").style.visibility = "visible";
                document.getElementById("main_data_selector").style.visibility = "visible";
                lahee_build_game_selector();
                lahee_autoselect_based_on_most_recent_achievement();
                lahee_change_game();
                lahee_records_change_selected();
                lahee_set_page("page_achievements");
            } catch (e) {
                lahee_init_error(e);
            }
        }, 1000);
    } catch (e) {
        lahee_init_error(e);
    }
}

/**
 * @param [event] {?LaheeLivePingEvent}
 */
function lahee_update_game_status(event) {
    lahee_request("r=laheeuserinfo&user=" + lahee_current_user.UserName + "&gameid=" + lahee_current_game.ID).then(function (res) {
        var userinfo = new LaheeUserResponse(lahee_current_user.GameData[lahee_current_game.ID], res);
        var msg;

        if (userinfo.current_game_id == lahee_current_game.ID) {
            var last_ping_diff = new Date() - (userinfo.last_ping ?? userinfo.last_play);
            if (last_ping_diff < 600_000) {
                msg = userinfo.game_status + "\nPlaytime: " + userinfo.play_time.toStringWithoutMs();
            } else {
                msg = "Game last played: " + TimeSpan.fromMilliseconds(last_ping_diff).toShortString() + " ago";
            }
        } else {
            msg = "Not currently playing " + lahee_current_game.Title + ".";
        }
        document.getElementById("status_text").innerText = msg;

        if (!lahee_current_user.GameData[lahee_current_game.ID]) {
            lahee_current_user.GameData[lahee_current_game.ID] = new LaheeUserGameData(lahee_current_user, null); // temporarily create a virtual object not synced to the server, so we can display achievements
        }
        lahee_current_user.GameData[lahee_current_game.ID].Achievements = userinfo.achievements;

        if (event?.pingType != LaheePingType.Time) {
            lahee_build_achievements(lahee_current_user, lahee_current_game);
        }
    });
}

function lahee_change_game() {
    var user = lahee_data.getUserById(document.getElementById("user_select").value);
    var game = lahee_data.getGameById(document.getElementById("game_select").value);

    if (!user || !game) {
        console.error("Can't switch to undefined user/game: " + user?.ID + "," + game?.ID);
        return;
    }

    if (!user.AllowUse) {
        alert("An error occurred while trying to load save data for this user. Check LAHEE log files.");
        return;
    }

    var prev_user_id = lahee_current_user?.ID;

    lahee_current_user = user;
    lahee_current_game = game;

    document.getElementById("user_avatar").src = "../UserPic/" + user.UserName + ".png";
    document.getElementById("game_avatar").src = game.ImageIconURL;

    lahee_build_achievements(user, game);
    lahee_records_build_select(user, game);
    lahee_records_change_selected();
    lahee_update_game_status();
    if (prev_user_id != user.ID) {
        console.log("User changed (or first load), updating stats");
        lahee_create_stats(user);
    }
}

function lahee_autoselect_based_on_most_recent_achievement() {
    var last_user;
    var last_game;
    var last_time = 0;

    for (var u of lahee_data.users) {
        for (var ua of u.getAllAchievements()) {
            var time = ua.getLaterAchieveDate();
            if (time && time > last_time) {
                last_user = u;
                last_game = ua.getAchievementData()?.getGameData();
                last_time = time;
            }
        }
    }

    if (last_user != null && last_game != null) {
        document.getElementById("user_select").value = last_user.ID;
        document.getElementById("game_select").value = last_game.ID;
        console.log("Latest Achievement was from " + last_time + " from UID " + last_user + " in " + last_game);
    }
}

/**
 * @param user {LaheeUserData}
 * @param game {LaheeGameData}
 */
function lahee_build_achievements(user, game) {

    var ug = user.getUserGameData(game);

    var sort = document.getElementById("sort_select").value;
    var filter = document.getElementById("filter").value;
    var arr = game.getAllAchievements();

    if (filter) {
        filter = filter.toLowerCase();
        arr = arr.filter(a => {
            return a.Title.toLowerCase().includes(filter) ||
                a.Description.toLowerCase().includes(filter);
        });
    }
    arr.sort(function (a, b) {
        var ua = ug.getAchievementData(a.ID);
        if (sort == 1) { // locked
            if (!ua.AchieveDate && !ua.AchieveDateSoftcore) {
                return 0;
            }
            return 1;
        } else if (sort == 2) { // unlocked
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 0;
            }
            return 1;
        } else if (sort == 3) { // missable
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 1;
            }
            if (a.Type == LaheeAchievementType.missable) {
                return -1;
            }
            return 1;
        } else if (sort == 4) { // flagged
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 1;
            }
            if (ug?.FlaggedAchievements.includes(a.ID)) {
                return -1;
            }
            return 1;
        } else if (sort == 5) { // points
            return b.Points - a.Points;
        }
    });

    var content = "";
    if (localStorage.getItem("lahee_setting_ach_grouping") === "true" && game.AchievementSets.length > 1) {
        var rendered_achievement_sets = {};
        for (var set of game.AchievementSets) {
            rendered_achievement_sets[set.Title] = "";
        }
        for (var a of arr) {
            var ua = ug.getAchievementData(a);

            rendered_achievement_sets[a.Set.Title] += lahee_render_achievement(game, ug, a, ua);
        }

        for (var title of Object.keys(rendered_achievement_sets)) {
            content += `
                <p class="ach_set_header">${title}</p>
                <div class='ach_grid'>
                    ${rendered_achievement_sets[title]}
                </div>
            `;
        }
    } else {
        content += "<div class='ach_grid'>";

        for (var a of arr) {
            var ua = ug.getAchievementData(a);

            content += lahee_render_achievement(game, ug, a, ua);
        }

        content += "</div>";
    }


    document.getElementById("achievement_grid").innerHTML = content;

    var pt = 0;
    var max_pt = 0;
    for (var a of arr) {
        var ua = ug.getAchievementData(a);

        if (ua.Status > 0) {
            pt += a.Points;
        }
        max_pt += a.Points;
    }
    var game_point_el = document.getElementById("game_point_progress");
    game_point_el.style.width = (pt / max_pt * 100) + "%";
    game_point_el.innerText = Math.floor(pt / max_pt * 100) + "%";
    document.getElementById("game_pt").innerText = pt.toLocaleString() + "/" + max_pt.toLocaleString();

    var total_pt = 0;
    lahee_data.getAllAchievements().forEach(a => {
        var ua = user.getAchievementData(a);
        if (ua && ua.Status > LaheeUserAchievementStatus.Locked) {
            total_pt += a.Points;
        }
    });
    document.getElementById("total_pt").innerText = total_pt.toLocaleString();

    document.getElementById("adetail_info").style.display = "block";

    // delete any bootstrap tooltips we may still be hovering on
    if (tooltipList) {
        for (var t of tooltipList) {
            t.dispose();
        }
    }

    // assign bootstrap tooltips
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl, {
        trigger: 'hover'
    }));
}

/**
 * @param game_id {number}
 * @param ach_id {number}
 */
function lahee_select_ach(game_id, ach_id) {
    var ach = lahee_data.getAchievementById(ach_id);

    if (!ach) {
        console.error("AID not found: " + ach_id);
        return;
    }

    var game = ach.getGameData();

    lahee_current_achievement = ach;

    var ugd = lahee_current_user.GameData[game_id];
    var ua = lahee_current_user.getAchievementData(ach);

    document.getElementById("adetail_img").innerHTML = lahee_render_achievement(game, ugd, ach, ua);
    document.getElementById("adetail_title").innerText = ach.Title;
    document.getElementById("adetail_desc").innerText = ach.Description;
    var type = ach.Type ?? "";
    if ((ach.Flags & LaheeAchievementFlags.Unofficial) != 0) {
        type += " (Unofficial)";
    }
    document.getElementById("adetail_type").innerText = type;
    document.getElementById("adetail_score").innerText = ach.Points.toString();

    var status = "Locked";
    var unlockDate = "Locked";
    var unlockTime = "Locked";
    if (ua) {
        if (ua.Status == LaheeUserAchievementStatus.HardcoreUnlock) {
            status = "Hardcore Unlocked";
        } else if (ua.Status == LaheeUserAchievementStatus.SoftcoreUnlock) {
            status = "Unlocked";
        }
        if (ua.Status != LaheeUserAchievementStatus.Locked) {
            unlockDate = new Intl.DateTimeFormat(undefined, {
                dateStyle: 'short',
                timeStyle: 'short'
            }).format(ua.getAchieveDate());
            unlockTime = ua.getAchievePlaytime().toStringWithoutMs();
        }
    }

    document.getElementById("adetail_status").innerText = status;
    document.getElementById("adetail_unlock").innerText = unlockDate;
    document.getElementById("adetail_unlock_pt").innerText = unlockTime;
    document.getElementById("comment_controls").style.display = "block";

    if (game_id != lahee_current_game.ID) {
        document.getElementById("game_select").value = game_id;
        lahee_change_game();
    }
    lahee_set_page('page_achievements');
    lahee_load_comments(ach_id);
}

function lahee_live_ticker_connect() {
    var socket = new WebSocket("ws://" + (window.location.hostname != "" ? window.location.hostname : "localhost") + ":8001");

    socket.onopen = function () {
        console.log("[LiveTicker] Connection established");
        document.getElementById("status_text").innerText = "LiveTicker: Connected. Waiting for game start.";
    };

    socket.onmessage = function (event) {
        console.log("[LiveTicker] Data received from server: " + event.data);
        lahee_live_ticker_update(JSON.parse(event.data));
    };

    socket.onclose = function (event) {
        if (event.wasClean) {
            console.log("[LiveTicker] Connection closed cleanly, code=" + event.code + " reason=" + event.reason);
        } else {
            console.log("[LiveTicker] Connection died");
        }
        document.getElementById("status_text").innerText = "LiveTicker: Reconnecting...";
        setTimeout(lahee_live_ticker_connect, 5000);
    };

    socket.onerror = function (error) {
        console.log("[LiveTicker] " + error);
    };
}

/**
 * @param data {string}
 */
function lahee_live_ticker_update(data) {
    var event = new LaheeLiveTickerEvent(data);
    if (event.type == "ping") {
        lahee_update_game_status(new LaheeLivePingEvent(data));
    } else if (event.type == "unlock") {
        lahee_play_unlock_sound(new LaheeLiveUnlockEvent(data));
    } else {
        console.warn("unknown event: " + event.type);
    }
}

/**
 * @param user {LaheeUserData}
 * @param game {LaheeGameData}
 */
function lahee_records_build_select(user, game) {
    var list_html = "";

    var game_leaderboards = game.getAllLeaderboards();
    var has_leaderboards = game_leaderboards.length != 0;

    if (has_leaderboards) {
        for (var le of game_leaderboards) {
            list_html += "<option value='" + le.ID + "'>" + le.Title + "</option>";
        }
    } else {
        list_html += "<option value=''>No entries exist for this game.</option>";
    }

    var lb = document.getElementById("lb_id");
    lb.innerHTML = list_html;
    lb.disabled = !has_leaderboards;

    var lt = document.getElementById("lb_table");
    lt.style.display = has_leaderboards ? "table" : "none";
}

function lahee_records_change_selected() {

    var lb_id = document.getElementById("lb_id").value;

    var ul = lahee_current_user.getLeaderboardScoresFor(lb_id);
    var gl = lahee_current_game.getLeaderboard(lb_id);
    if (!gl) {
        console.error("leaderboard not found: " + lb_id);
        return;
    }

    var sort = document.getElementById("leaderboard_sort_select").value;
    for (var i = 0; i < ul.length; i++) {
        ul[i]._presort_index = i;
    }
    ul.sort(function (a, b) {
        if (sort == 0) { // best
            return b.Score - a.Score;
        } else if (sort == 1) { // recent
            return b.RecordDate - a.RecordDate;
        }
    });

    var list = "";

    if (ul.length > 0) {
        var format = Intl.DateTimeFormat(undefined, {dateStyle: 'short', timeStyle: 'short'});
        for (var e of ul) {
            list += `
            <tr>
                <td>${e._presort_index + 1}</td>
                <td>${e.Score.toLocaleString()}</td>
                <td>${format.format(e.RecordDate)}</td>
                <td>${e.PlayTime.toStringWithoutMs()}</td>
            </tr>
            `;
        }
    } else {
        list = "<tr><td colspan='4'>No scores recorded.</td></tr>";
    }

    document.getElementById("lb_content").innerHTML = list;
    document.getElementById("lb_desc").innerHTML = gl.Description;
}

/**
 * @param event {LaheeLiveUnlockEvent}
 */
function lahee_play_unlock_sound(event) {
    var ach = lahee_current_game.getAchievementById(event.userAchievementData.AchievementID);

    if (ach) {
        lahee_select_ach(event.gameId, ach.ID);

        if (Notification.permission == "granted") {
            new Notification("Achievement Unlocked!", {body: ach.Title + " (" + ach.Points + ")", icon: ach.BadgeURL});
            lahee_audio_play("162482__kastenfrosch__achievement.mp3");
        }
    }
}

/**
 * @param audio {string}
 */
function lahee_audio_play(audio) {
    if (Date.now() < lahee_last_audio + 1000) {
        console.log("not playing audio, too close to previous audio");
        return;
    }
    try {
        var sound = new Audio("sounds/" + audio);
        sound.play().catch(e => console.warn("Failed playing audio", e));
    } catch (e) {
        console.warn("Failed playing audio", e);
    }
    lahee_last_audio = Date.now();
}

/**
 * @param ach_id {number}
 */
function lahee_load_comments(ach_id) {
    var str = "";

    if (lahee_data.comments) {
        var comments = lahee_data.comments.sort(function (a, b) {
            return b.Submitted - a.Submitted;
        });
        for (var c of comments) {
            if (c.AchievementID == ach_id) {
                str += `<div>
                <hr />
                <b>${c.IsLocal ? c.User : "<i>" + c.User + "</i>"}:</b> <a class="small" href="javascript:lahee_delete_comment('${c.LaheeUUID}')">Delete</a><br />
                <p>${c.CommentText.replaceAll("\n", "<br />")}</p>
            </div>
            `;
            }
        }
    }

    var cc = document.getElementById("comment_container");
    cc.innerHTML = str;
    cc.style.display = str != "" ? "block" : "none";
}

function lahee_show_comment_editor() {
    lahee_popup = new bootstrap.Modal(document.getElementById('writeCommentModal'), {});
    lahee_popup.show();
    var editor = document.getElementById("comment_body");
    editor.value = "";
    setTimeout(function () {
        editor.focus();
    }, 100);
}

function lahee_write_comment() {
    lahee_request("r=laheewritecomment&user=" + lahee_current_user.UserName + "&gameid=" + lahee_current_game.ID + "&aid=" + lahee_current_achievement.ID + "&comment=" + encodeURIComponent(document.getElementById("comment_body").value)).then(function (ret) {
        var result = new LaheeFetchCommentsResponse(ret);
        if (result.Success) {
            lahee_data.comments = result.Comments;
            lahee_load_comments(lahee_current_achievement.ID);
            lahee_popup.hide();
        } else {
            alert("Error occurred while adding comment: " + result.Error);
        }
    }).catch(function (e) {
        console.error(e);
        alert("Error occurred while adding comment: " + e);
    });
}

function lahee_download_comments() {
    document.getElementById("ra_download_btn").disabled = true;
    lahee_request("r=laheefetchcomments&user=" + lahee_current_user.UserName + "&gameid=" + lahee_current_game.ID + "&aid=" + lahee_current_achievement.ID).then(function (ret) {
        document.getElementById("ra_download_btn").disabled = false;
        var result = new LaheeFetchCommentsResponse(ret);
        if (result.Success) {
            lahee_data.comments = result.Comments;
            lahee_load_comments(lahee_current_achievement.ID);
        } else {
            alert("Error occurred while downloading RA data: " + result.Error);
        }
    }).catch(function (e) {
        console.error(e);
        document.getElementById("ra_download_btn").disabled = false;
        alert("Error occurred while downloading RA data: " + e);
    });
}

// noinspection JSUnusedGlobalSymbols
/**
 * @param comment_uuid {string}
 */
function lahee_delete_comment(comment_uuid) {
    if (confirm("Are you sure that you want to delete a comment?")) {
        lahee_request("r=laheedeletecomment&uuid=" + comment_uuid + "&gameid=" + lahee_current_game.ID).then(function (ret) {
            var result = new LaheeFetchCommentsResponse(ret);
            if (result.Success) {
                lahee_data.comments = result.Comments;
                lahee_load_comments(lahee_current_achievement.ID);
            } else {
                alert("Error occurred while deleting comment: " + result.Error);
            }
        }).catch(function (e) {
            console.error(e);
            alert("Error occurred while deleting comment: " + e);
        });
    }
}

function lahee_comment_editor_onkeyup(event) {
    if (event.ctrlKey && event.which == 13) {
        lahee_write_comment();
    } else if (event.which == 17) {
        lahee_popup.hide();
    }
}

function lahee_flag_important() {
    lahee_request("r=laheeflagimportant&user=" + lahee_current_user.UserName + "&gameid=" + lahee_current_game.ID + "&aid=" + lahee_current_achievement.ID).then(function (ret) {
        var result = new LaheeFlagAchievementResponse(ret);
        if (result.Success) {
            lahee_current_user.GameData[lahee_current_game.ID].FlaggedAchievements = result.Flagged;

            lahee_build_achievements(lahee_current_user, lahee_current_game);
            lahee_select_ach(lahee_current_game.ID, lahee_current_achievement.ID);
        } else {
            throw new Error(result.Error);
        }
    }).catch(function (e) {
        console.error(e);
        alert("Error communicating with LAHEE: " + e);
    });
}


/**
 * @param user {LaheeUserData}
 */
function lahee_create_stats(user) {
    if (!user || !user.AllowUse || !user.GameData || Object.keys(user.GameData).length == 0) {
        console.warn("Cannot create stats, no user data");
        document.getElementById("stats_unavailable").style.display = "block";
        document.getElementById("stats_available").style.display = "none";
        return;
    }

    document.getElementById("stats_unavailable").style.display = "none";
    document.getElementById("stats_available").style.display = "block";

    var total_time = 0;
    var game_counts = [0, 0, 0, 0, 0];
    var ach_counts = [0, 0, 0];
    var score_counts = [0, 0, 0];
    var table_str = "";

    var longest_pt = {id: 0, v: 0};
    var shortest_pt = {id: 0, v: 9999999999999};
    var fastest_100 = {id: 0, v: 9999999999999};
    var slowest_100 = {id: 0, v: 0};
    var fastest_beat = {id: 0, v: 9999999999999};
    var slowest_beat = {id: 0, v: 0};
    var first_play = new Date();

    game_counts[0] = lahee_data.games.length;

    for (var ug of user.getAllGameData()) {
        var game = ug.getGameData();
        console.log("Checking: " + game?.Title + "(" + ug.GameID + ")");
        var user_achievement_arr = ug.getAllAchievements();
        /** @type {Array.<LaheeAchievementData>} */
        var game_achievement_arr = game?.getAllAchievements() ?? [];

        var total_achievements = game_achievement_arr.length;
        var hardcore_achievements = user_achievement_arr.filter(a => a.Status == 2).length;
        var softcore_achievements = user_achievement_arr.filter(a => a.Status == 1).length;
        var completion_ids = game_achievement_arr.filter(a => a.Type == LaheeAchievementType.win_condition).map(a => a.ID);

        var playtime_ms = 0;
        if (ug.PlayTimeApprox) {
            playtime_ms = ug.PlayTimeApprox.valueOf();
        }

        if (playtime_ms > longest_pt.v) {
            longest_pt.id = ug.GameID;
            longest_pt.v = playtime_ms;
        }
        if (playtime_ms < shortest_pt.v && playtime_ms > 0) {
            shortest_pt.id = ug.GameID;
            shortest_pt.v = playtime_ms;
        }
        if ((hardcore_achievements + softcore_achievements) >= total_achievements && playtime_ms < fastest_100.v && playtime_ms > 0) {
            fastest_100.id = ug.GameID;
            fastest_100.v = playtime_ms;
        }
        if ((hardcore_achievements + softcore_achievements) >= total_achievements && playtime_ms > slowest_100.v && playtime_ms > 0) {
            slowest_100.id = ug.GameID;
            slowest_100.v = playtime_ms;
        }

        var beat = null;
        for (var completion_achievement_id of completion_ids) {
            var ua = ug.Achievements[completion_achievement_id];
            if (ua && ua.Status > 0) {
                var at = ua.getAchievePlaytime();
                if (beat == null || beat.compareTo(at) < 0) {
                    beat = at;
                }
            }
        }

        if (beat != null) {
            if (beat.valueOf() < fastest_beat.v && beat.valueOf() > 0 && playtime_ms > 0) {
                fastest_beat.id = ug.GameID;
                fastest_beat.v = beat.valueOf();
            }
            if (beat.valueOf() > slowest_beat.v) {
                slowest_beat.id = ug.GameID;
                slowest_beat.v = beat.valueOf();
            }
        }

        if (ug.FirstPlay) {
            var this_first_play = new Date(ug.FirstPlay);
            if (this_first_play < first_play) {
                first_play = this_first_play;
            }
        }

        total_time += playtime_ms;

        var status = "";
        game_counts[1]++;
        if (user_achievement_arr.filter(a => completion_ids.includes(a.AchievementID) && a.Status > 0).length > 0) {
            status = "Beaten";
            game_counts[2]++;
        }
        if (hardcore_achievements + softcore_achievements == total_achievements) {
            status = "Completed";
            game_counts[3]++;
        }
        if (hardcore_achievements == total_achievements) {
            status = "Mastered";
            game_counts[4]++;
        }

        ach_counts[0] += total_achievements;
        ach_counts[1] += softcore_achievements + hardcore_achievements;
        ach_counts[2] += hardcore_achievements;

        if (game) {
            var game_pt_total = 0;
            var game_pt_hardcore = 0;
            var game_pt_softcore = 0;
            for (var a of game.getAllAchievements()) {
                game_pt_total += a.Points;
                var ua = ug.Achievements[a.ID];
                if (ua && ua.Status > 0) {

                    if (ua.Status == 2) {
                        game_pt_hardcore += a.Points;
                    } else if (ua.Status == 1) {
                        game_pt_softcore += a.Points;
                    }

                    score_counts[ua.Status] += a.Points;
                }

            }

            score_counts[0] += game_pt_total;

            table_str += `
                <tr>
                    <td><img src="${game.ImageIconURL}" height="64" /></td>
                    <td>${game.Title}</td>
                    <td class="text-center">
                        ${softcore_achievements + hardcore_achievements} / ${total_achievements}
                        <div class="progress">
                            <div class="progress-bar bg-hardcore" role="progressbar" style="width: ${hardcore_achievements / total_achievements * 100}%"></div>
                            <div class="progress-bar bg-softcore" role="progressbar" style="width: ${softcore_achievements / total_achievements * 100}%"></div>
                        </div>
                    </td>
                    <td class="text-center">
                        ${(game_pt_softcore + game_pt_hardcore).toLocaleString()} / ${game_pt_total.toLocaleString()}
                        <div class="progress">
                            <div class="progress-bar bg-hardcore" role="progressbar" style="width: ${game_pt_hardcore / game_pt_total * 100}%"></div>
                            <div class="progress-bar bg-softcore" role="progressbar" style="width: ${game_pt_softcore / game_pt_total * 100}%"></div>
                        </div>
                    </td>
                    <td>${status}</td>
                    <td>${ug.FirstPlay.toLocaleString()}</td>
                    <td>${ug.PlayTimeApprox.toStringWithoutMs()}</td>
                </tr>
            `;
        }
    }

    lahee_stats_render_game("l", user, longest_pt.id);
    lahee_stats_render_game("s", user, shortest_pt.id);
    lahee_stats_render_game("f1", user, fastest_100.id);
    lahee_stats_render_game("s1", user, slowest_100.id);
    lahee_stats_render_game("fb", user, fastest_beat.id, fastest_beat.v);
    lahee_stats_render_game("sb", user, slowest_beat.id, slowest_beat.v);

    var tt = TimeSpan.fromMilliseconds(total_time);
    document.getElementById("total_time").innerText = tt.toStringWithoutMs() + " (" + Math.floor(tt.totalHours) + "h.)";
    document.getElementById("total_counts").innerText = game_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_ach").innerText = ach_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_score").innerText = score_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_started").innerText = first_play.toLocaleDateString();
    document.getElementById("stats_table").innerHTML = table_str;

    lahee_stats_render_milestones(user);
    lahee_stats_render_meta_achievements(user);
}

/**
 * @param suffix {string}
 * @param user {LaheeUserData}
 * @param game_id {number}
 * @param [time] {?number}
 */
function lahee_stats_render_game(suffix, user, game_id, time) {
    var game = lahee_data.getGameById(game_id);
    var ug = user?.GameData[game_id];

    var total_achievements = game?.getAllAchievements().length ?? -1;
    var achievements_softcore = game && ug ? Object.values(ug.Achievements).filter(a => a.Status == 1).reduce((partialSum, a) => partialSum + a, 0) : -2;
    var achievements_hardcore = game && ug ? Object.values(ug.Achievements).filter(a => a.Status == 2).reduce((partialSum, a) => partialSum + a, 0) : -2;
    var status = 0;
    if (total_achievements == achievements_hardcore) {
        status = 2; // mastered
    } else if (total_achievements == achievements_softcore) {
        status = 1; // completed
    }

    var img = document.getElementById("game_img_" + suffix);
    for (var i = 0; i < 4; i++) {
        img.classList.remove("ach_status_" + i);
    }
    img.classList.add("ach_status_" + status);
    img.src = game ? game.ImageIconURL : "";

    document.getElementById("game_title_" + suffix).innerText = game ? game.Title : (game_id > 0 ? "Unknown Game: " + game_id : "No Data");
    document.getElementById("game_time_" + suffix).innerText = time ? TimeSpan.fromMilliseconds(time).toStringWithoutMs() : (ug ? ug.PlayTimeApprox.toStringWithoutMs() : "--:--:--");
}

/**
 * @param user {LaheeUserData}
 */
function lahee_stats_render_milestones(user) {
    var all_user_achievements = user.getAllAchievements().filter(a => a.Status != LaheeUserAchievementStatus.Locked).sort(function (a, b) {
        return a.getLaterAchieveDate() - b.getLaterAchieveDate();
    });
    var milestone_html = "";

    const milestones = [1, 2, 5, 10, 15, 20, 25, 50, 100, 123, 150, 200, 250, 300, 350, 400, 450, 500, 600, 666, 700, 777, 800, 900, 1000, 1234, 1337, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 6000, 6666, 7000, 7777, 8000, 9000, 10000];
    for (var i of milestones) {
        if (all_user_achievements[i - 1]) {
            var ua = all_user_achievements[i - 1];

            var game = ua.UserGame.getGameData();
            var ach = ua.getAchievementData();

            milestone_html += `
                <tr>
                    <td><img src="${game?.ImageIconURL}" height="64" /></td>
                    <td>${lahee_render_achievement(game, ua.UserGame, ach, ua)}</td>
                    <td>${ach?.Title ?? ("Unknown Achievement: " + ua.AchievementID)}<br /><small>${game?.Title ?? "Unknown Game"}</small></td>
                    <td>${ua.getLaterAchieveDate().toLocaleString()}</td>
                    <td>${ua.getLaterPlaytime().toStringWithoutMs()}</td>
                    <td>#${i}</td>
                </tr>
            `;
        }
    }
    document.getElementById("milestones_table").innerHTML = milestone_html;
}

// object returned from any of the lahee_check_meta_* functions
class LaheeMetaResult {
    /**
     * the achievement that fulfills this meta-achievement
     * @type {LaheeUserAchievementData}
     */
    user_achievement;
    /**
     * the name of the meta-achievement
     * @type {string}
     */
    name;
    /**
     * explanation of the meta-achievement
     * @type {string}
     */
    description;
    /**
     * an array of achievements related to fulfilling the condition
     * @type {?Array.<LaheeUserAchievementData>}
     */
    related_user_achievements;

    constructor(ua, name, description, rel_ua = null) {
        this.user_achievement = ua;
        this.name = name;
        this.description = description;
        this.related_user_achievements = rel_ua;
    }
}

function lahee_stats_render_meta_achievements(user) {
    // any meta functions must return LaheeMetaResult
    var meta_conditions = [
        lahee_check_meta_first,
        lahee_check_meta_nut,
        lahee_check_meta_grind,
        lahee_check_meta_combo
    ];

    var meta_html = "";

    for (var cond of meta_conditions) {
        var meta_data = cond(user);
        if (meta_data) {
            var ua = meta_data.user_achievement;
            var ug = ua.UserGame;
            var game = ua.UserGame.getGameData();
            var ach = ua.getAchievementData();

            var related_html = "";
            for (var related_ua of meta_data.related_user_achievements ?? []) {
                var related_ach = game?.getAchievementById(related_ua.AchievementID);
                related_html += lahee_render_achievement(game, ug, related_ach, ua);
            }

            meta_html += `
                <tr>
                    <td>${meta_data.name}<br /><small>${meta_data.description}</small></td>
                    <td><img src="${game?.ImageIconURL}" height="64" /></td>
                    <td>${lahee_render_achievement(game, ug, ach, ua)}</td>
                    <td>${ach?.Title ?? ("Unknown Achievement: " + ua.AchievementID)}<br /><small>${game?.Title ?? "Unknown Game"}</small></td>
                    <td>${related_html}</td>
                    <td>${ua.getLaterAchieveDate().toLocaleString()}</td>
                    <td>${ua.getLaterPlaytime().toStringWithoutMs()}</td>
                </tr>
            `;
        }
    }

    document.getElementById("meta_table").innerHTML = meta_html;
}

/**
 * @param user {LaheeUserData}
 * @returns {LaheeMetaResult|null}
 */
function lahee_check_meta_first(user) {
    var all_user_achievements_sorted = user.getAllUnlockedAchievements().sort(function (a, b) {
        return a.getAchieveDate() - b.getAchieveDate();
    });

    if (all_user_achievements_sorted[0]) {
        return new LaheeMetaResult(all_user_achievements_sorted[0], "The First", "The very first achievement you have obtained.");
    }

    return null;
}

function lahee_check_meta_nut(user) {
    var all_user_achievements = user.getAllAchievements();

    if (all_user_achievements.length < 2) { // condition can't be met if we have less than 2 achievements
        return null;
    }

    var highest_diff = 0; // currently highest time difference between two achievements
    /**
     * achievement before our current top time difference candidate
     * @type {?LaheeUserAchievementData}
     */
    var highest_diff_previous_ua = null; //
    /**
     * current highest time difference achievement
     * @type {?LaheeUserAchievementData}
     */
    var highest_diff_ua = null;

    for (const is_hardcore of [false, true]) {

        var previous_ua = null;
        /** @type {Array.<LaheeUserAchievementData>} */
        var all_user_achievements_sorted = all_user_achievements.slice().sort(function (a, b) {
            return is_hardcore ? a.AchieveDate - b.AchieveDate : a.AchieveDateSoftcore - b.AchieveDateSoftcore;
        });

        for (var ua of all_user_achievements_sorted) {

            var playtime = (is_hardcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).valueOf();
            if (playtime <= 0) {
                continue;
            }

            var previous_playtime = previous_ua ? (is_hardcore ? previous_ua.AchievePlaytime : previous_ua.AchievePlaytimeSoftcore).valueOf() : 0;

            var diff = Math.abs(playtime - previous_playtime);
            if (diff > highest_diff) {
                var game1 = ua.UserGame.getGameData();
                var game2 = previous_ua?.UserGame.getGameData();
                if (!game2 || game1.ID == game2.ID) {
                    console.log("new nut: " + ua.AchievementID + " from " + previous_ua?.AchievementID + ", diff=" + diff);
                    highest_diff = diff;
                    highest_diff_previous_ua = previous_ua;
                    highest_diff_ua = ua;
                }
            }

            previous_ua = ua;
        }
    }

    if (highest_diff_ua) {
        var ach = highest_diff_previous_ua.getAchievementData();
        return new LaheeMetaResult(highest_diff_ua, "The Nut", "The achievement with the longest time between the previous achievement and this one.<br>(You: " + TimeSpan.fromMilliseconds(highest_diff).toStringWithoutMs() + " since " + (ach.Title ?? "Unknown Achievement: " + highest_diff_previous_ua.AchievementID) + ")", [highest_diff_previous_ua]);
    }

    return null;
}

function lahee_check_meta_grind(user) {
    var all_user_achievements_sorted = user.getAllUnlockedAchievements().sort(function (a, b) {
        return a.getAchieveDate() - b.getAchieveDate();
    });

    var current_highest_points = 0;
    var current_highest_playtime = 0;
    var current_highest_ua = null;

    for (var ua of all_user_achievements_sorted) {
        var ach = ua.getAchievementData();
        var playtime = ua.getAchievePlaytime().valueOf();

        if ((ach && ach.Points >= current_highest_points) && (!current_highest_ua || playtime > current_highest_playtime)) {
            current_highest_points = ach.Points;
            current_highest_playtime = playtime;
            current_highest_ua = ua;
        }
    }

    if (current_highest_ua != null) {
        return new LaheeMetaResult(current_highest_ua, "The Grind", "The achievement with the highest point value obtained after the longest amount of play time.<br>(You: " + current_highest_points + " Points)");
    }

    return null;
}

function lahee_check_meta_combo(user) {
    var all_user_achievements = user.getAllAchievements();

    if (all_user_achievements.length < 2) { // condition can't be met if we have less than 2 achievements
        return null;
    }

    var highest_combo_count = 1; // combo needs to be longer than 1
    /**
     * achievements of current best combo
     * @type {Array.<LaheeUserAchievementData>}
     */
    var highest_combo_ua = [];
    var highest_combo_time = 0; // time length how long the achievement combo was held

    for (const is_hardcore of [false, true]) {

        /** @type {Array.<LaheeUserAchievementData>} */
        var all_user_achievements_sorted = all_user_achievements.sort(function (a, b) {
            return is_hardcore ? a.AchievePlaytime - b.AchievePlaytime : a.AchievePlaytimeSoftcore - b.AchievePlaytimeSoftcore;
        });

        var current_combo_count = 0;
        /** @type {Array.<LaheeUserAchievementData>}  */
        var current_combo_ua = [];
        var current_combo_diff_total = 0;
        /** @type {?LaheeUserAchievementData} */
        var previous_ua = null;

        for (var ua of all_user_achievements_sorted) {

            var playtime = (is_hardcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).valueOf();
            if (playtime <= 0) {
                continue;
            }

            var previous_playtime = previous_ua ? (is_hardcore ? previous_ua.AchievePlaytime : previous_ua.AchievePlaytimeSoftcore).valueOf() : 0;

            var diff = Math.abs(playtime - previous_playtime);
            if (diff < 1000 * 60 * 10) { // at most 10 minutes between achievements
                current_combo_count++;
                current_combo_diff_total += diff;
                current_combo_ua.push(ua);
            } else {

                if (current_combo_count > highest_combo_count) {
                    highest_combo_count = current_combo_count;
                    highest_combo_ua = current_combo_ua;
                    highest_combo_time = current_combo_diff_total;
                }

                current_combo_count = 1;
                current_combo_ua = [ua];
                current_combo_diff_total = 0;
            }

            previous_ua = ua;
        }
    }

    if (highest_combo_count > 1) {

        console.log("combo timestamps: ", highest_combo_ua.map(ua => ua.getAchieveDate()));

        return new LaheeMetaResult(highest_combo_ua[0], "The Combo", "The longest combo of achievements within 10 minutes of each other.<br>(You: " + highest_combo_count + " within " + TimeSpan.fromMilliseconds(highest_combo_time).toStringWithoutMs() + ")", highest_combo_ua);
    }

    return null;
}

/**
 * @param [game] {?LaheeGameData}
 * @param [ug] {?LaheeUserGameData}
 * @param [a] {?LaheeAchievementData}
 * @param [ua] {?LaheeUserAchievementData}
 * @param [size] {?number}
 * @returns {string}
 */
function lahee_render_achievement(game, ug, a, ua, size) {
    var status = ua?.Status ?? LaheeUserAchievementStatus.Locked;
    if (!a) {
        var id = ua?.AchievementID ?? a.ID ?? 0;
        return `<img src="/Badge/00000.png" class="ach ach_status_0" loading="lazy" data-bs-html="true" data-bs-toggle="tooltip" data-bs-title="<b>Unknown Achievement</b><hr />Unknown Achievement ID ${id}" ${size ? "width='" + size + "'" : ""} />`;
    }
    return `<img src="${status != LaheeUserAchievementStatus.Locked ? a.BadgeURL : a.BadgeLockedURL}" class="ach ach_type_${a.Type} ach_status_${status} ach_flags_${a.Flags} ${ug?.FlaggedAchievements?.includes(a.ID) ? "ach_flag_important" : ""}" onclick="lahee_select_ach(${game?.ID ?? 0}, ${a.ID});" loading="lazy" data-bs-html="true" data-bs-toggle="tooltip" data-bs-title="<b>${a.Title.replaceAll("\"", "&quot;")}</b> (${a.Points})<hr />${a.Description.replaceAll("\"", "&quot;")}" ${size ? "width='" + size + "'" : "width='64' height='64'"} />`;
}

function lahee_show_code_popup() {
    var ach = lahee_current_achievement;
    var game = ach.getGameData();
    if (!ach || !game) {
        return;
    }

    var ug = lahee_current_user.getUserGameData(game);
    var ua = ug.getAchievementData(ach);

    if (!ach.MemAddr) {
        alert("This achievement has no code.");
        return;
    }

    document.getElementById("download_ach_code_btn").disabled = true;
    lahee_request("r=laheeachievementcode&gameid=" + game.ID + "&aid=" + lahee_current_achievement.ID).then(function (ret) {
        var result = new LaheeAchievementCodeResponse(ret);
        document.getElementById("download_ach_code_btn").disabled = false;
        if (result.Success) {
            lahee_update_code_popup(game, ug, ach, ua, result.CodeNotes, result.TriggerGroups);
            lahee_popup = new bootstrap.Modal(document.getElementById('codeModal'), {});
            lahee_popup.show();
        } else {
            alert("Error occurred while loading data: " + result.Error);
        }
    }).catch(function (e) {
        document.getElementById("download_ach_code_btn").disabled = false;
        alert("Error occurred while loading data: " + e);
    });
}

/**
 * @param game {LaheeGameData}
 * @param ug {LaheeUserGameData}
 * @param a {LaheeAchievementData}
 * @param ua {LaheeUserAchievementData}
 * @param code_notes {Array.<LaheeCodeNote>}
 * @param groups {any}
 */
function lahee_update_code_popup(game, ug, a, ua, code_notes, groups) {
    var html = "";

    for (var i = 0; i < groups.length; i++) {
        var group = groups[i];
        var ht = i == 0 ? "All the conditions in the core group must be true" : "When using Alt groups, for the achievement to trigger, all the conditions in the Core group MUST be true.&#013;And then all the conditions of ANY Alt group must be true.&#013;In other words, each Alt group uses OR logic.";
        html += "<tr><td colspan='7' class='text-center fw-bold'><span class='hoverable_text' title='" + ht + "'>Group " + (i + 1) + " (" + (i == 0 ? "Core" : "Alt " + i) + ")</span></td></tr>";

        for (var req of group.Requirements) {

            html += `<tr>
                <td><span class="hoverable_text" title="${req.getTriggerTypeDescription()}">${req.getTriggerTypeName()}</span></td>
                <td>
                    <span class="hoverable_text" title="${req.Left.getFieldTypeDescription()}">${req.Left.getFieldTypeName()}</span>
                    <span class="hoverable_text" title="${req.Left.getSizeDescription()}">${req.Left.getSizeName()}</span>
                </td>
                <td>${lahee_trigger_render_value(code_notes, req.Left)}</td>
                <td>${req.getOperatorText()}</td>
                <td>
                    <span class="hoverable_text" title="${req.Right.getFieldTypeDescription()}">${req.Right.getFieldTypeName()}</span>
                    <span class="hoverable_text" title="${req.Right.getSizeDescription()}">${req.Right.getSizeName()}</span>
                </td>
                <td>${lahee_trigger_render_value(code_notes, req.Right)}</td>
                <td>${req.HitCount}</td>
            </tr>`;
        }

    }

    document.getElementById("codeModalTitle").innerHTML = lahee_render_achievement(game, ug, a, ua, 32) + " " + a.Title + ": Achievement Code";
    document.getElementById("ach_code_desc").innerHTML = a.Description;
    document.getElementById("ach_code_table").innerHTML = html;
}

function lahee_find_code_note_text(code_notes, addr) {
    if (!code_notes) {
        return null;
    }
    for (var cn of code_notes) {
        if (Number(cn.Address) == addr) {
            return cn.Note;
        }
    }
    return null;
}

function lahee_trigger_render_value(code_notes, field) {
    var cn = null;
    if (field.IsMemoryReference) {
        cn = lahee_find_code_note_text(code_notes, field.Value);
    }

    var val;
    var addr;
    if (field.IsMemoryReference) {
        addr = "0x" + field.Value.toString(16);
        val = "<i>" + addr + "</i>";
    } else {
        val = field.Float != 0 ? field.Float : field.Value;
        addr = field.Float == 0 ? "0x" + val.toString(16) : null;
    }

    if (cn) { // If we have code notes, cut down the text a bit for inline display
        var was_cut = false;
        var string_cutoff = cn.indexOf("\n"); // only show first line if multiline
        if (string_cutoff > 0) {
            val = cn.substring(0, string_cutoff);
            was_cut = true;
        } else {
            val = cn;
        }
        string_cutoff = val.indexOf(". "); // only show first sentence if they are not seperated by newlines
        if (string_cutoff > 0) {
            val = val.substring(0, string_cutoff);
            was_cut = true;
        }
        var tcn = addr + ": " + cn.replaceAll("\'", "&#39;").replaceAll("\n", "&#013;");
        var ecn = cn.replaceAll("\'", "&#39;").replaceAll("\n", "<br />");
        if (was_cut) {
            return "<a class='hoverable_text' title='" + tcn + "' onclick='lahee_show_extended_cn(`" + addr + "`, `" + ecn + "`)'>" + val + "</a>";
        } else {
            return "<span class='hoverable_text' title='" + tcn + "'>" + val + "</span>";
        }
    } else if (addr && !field.IsMemoryReference) {
        return "<span class='hoverable_text' title='" + addr + "'>" + val + "</span>";
    } else {
        return val;
    }
}

function lahee_show_extended_cn(addr, text) {
    document.getElementById("extendedCodeNoteTitle").innerHTML = addr;
    document.getElementById("extendedCodeNoteModalText").innerHTML = text;
    lahee_popup_2 = new bootstrap.Modal(document.getElementById('extendedCodeNoteModal'), {});
    lahee_popup_2.show();
}

function lahee_build_game_selector() {
    var ru = document.getElementById("user_select").value;
    var user = lahee_data.getUserById(ru);

    var now = new Date();
    var game_list = lahee_data.games.slice();

    var select_html = "";

    if (user) {

        select_html += "<optgroup label='Recently Played'>";
        for (var game of game_list) {
            var ug = user.getUserGameData(game);
            if (now - ug.LastPlay < 30 * 24 * 60 * 60 * 1000) {
                select_html += "<option value='" + game.ID + "' " + (lahee_current_game?.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
                game_list = game_list.filter(g => g.ID != game.ID);
            }
        }
        select_html += "</optgroup>";

        select_html += "<optgroup label='Unplayed'>";
        for (var game of game_list) {
            var ug = user.getUserGameData(game);
            if (!ug.Achievements || Object.keys(ug.Achievements).length == 0) {
                select_html += "<option value='" + game.ID + "' " + (lahee_current_game?.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
                game_list = game_list.filter(g => g.ID != game.ID);
            }
        }
        select_html += "</optgroup>";

        // calculate beaten/completed games first, so unfinished doesn't take priority
        var games_completed = "<optgroup label='Completed'>";
        for (var game of game_list) {
            var ug = user.getUserGameData(game);
            var ach_arr = game.getAllAchievements();
            if (ug.Achievements && Object.keys(ug.Achievements).length >= ach_arr.length) {
                games_completed += "<option value='" + game.ID + "' " + (lahee_current_game?.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
                game_list = game_list.filter(g => g.ID != game.ID);
            }
        }
        games_completed += "</optgroup>";

        var games_beaten = "<optgroup label='Beaten'>";
        for (var game of game_list) {
            var ach_arr = game.getAllAchievements();
            var completion_ids = ach_arr.filter(a => a.Type == LaheeAchievementType.win_condition).map(a => a.ID) ?? [];
            var ug = user.getUserGameData(game);
            if (Object.values(ug.Achievements).filter(a => completion_ids.includes(a.AchievementID) && a.Status > 0).length > 0) {
                games_beaten += "<option value='" + game.ID + "' " + (lahee_current_game?.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
                game_list = game_list.filter(g => g.ID != game.ID);
            }
        }
        games_beaten += "</optgroup>";

        select_html += "<optgroup label='Unfinished'>";
        for (var game of game_list) {
            var ug = user.getUserGameData(game);
            var ach_arr = game.getAllAchievements();
            if (!ug.Achievements || Object.keys(ug.Achievements).length < ach_arr.length) {
                select_html += "<option value='" + game.ID + "' " + (lahee_current_game?.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
                game_list = game_list.filter(g => g.ID != game.ID);
            }
        }
        select_html += "</optgroup>";

        select_html += games_beaten;
        select_html += games_completed;

    } else {

        console.warn("cannot create game selector based on userdata because no userdata is available");

        for (var game of lahee_data.games) {
            select_html += "<option value='" + game.ID + "'>" + game.Title + "</option>";
        }

    }

    document.getElementById("game_select").innerHTML = select_html;
}

function lahee_settings_load() {
    document.getElementById("lahee_setting_ach_grouping").checked = localStorage.getItem("lahee_setting_ach_grouping") === "true";
    document.getElementById("lahee_setting_ach_no_margin").checked = localStorage.getItem("lahee_setting_ach_no_margin") === "true";
    document.getElementById("lahee_setting_split_sizes").value = localStorage.getItem("lahee_setting_split_sizes");
}

function lahee_settings_save() {
    localStorage.setItem("lahee_setting_ach_grouping", document.getElementById("lahee_setting_ach_grouping").checked);
    localStorage.setItem("lahee_setting_ach_no_margin", document.getElementById("lahee_setting_ach_no_margin").checked);

    bootstrap.Toast.getOrCreateInstance(document.getElementById("toast_saved")).show();
}

function lahee_settings_reset() {
    localStorage.clear();
    window.location.reload();
}

function lahee_settings_apply() {
    var no_margin_enabled = localStorage.getItem("lahee_setting_ach_no_margin");
    var grid = document.getElementById("achievement_grid");
    if (no_margin_enabled === "true") {
        grid.style.margin = "0";
        grid.style.padding = "0";
    } else {
        grid.style.removeProperty("margin");
        grid.style.removeProperty("padding");
    }

    if (lahee_data) {
        lahee_change_game();
    }
}