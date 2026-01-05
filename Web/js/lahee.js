// noinspection EqualityComparisonWithCoercionJS,JSUnresolvedReference,HtmlRequiredAltAttribute,ExceptionCaughtLocallyJS,JSDuplicatedDeclaration

var LAHEE_URL;
var lahee_data;
var lahee_user;
var lahee_game;
var lahee_last_audio = 0;
var lahee_popup;
var lahee_popup_2;
var lahee_achievement;

var tooltipList;

function lahee_init() {
    if (Notification.permission == "default") {
        Notification.requestPermission();
    }
    lahee_audio_play("540121__jj_om__blank-sound.ogg");

    LAHEE_URL = "http://" + window.location.host + "/dorequest.php";
    lahee_request("r=laheeinfo", lahee_postinit, function (e) {
        lahee_init_error(e);
    });
}

function lahee_init_error(e) {
    console.error(e);
    document.getElementById("page_loading").innerHTML = `
        <p>An error has occurred while trying to load data.</p>
        <p><small>${e}</small></p>
        <p><input type="button" value="Retry" class="btn btn-primary" onclick="window.location.reload();" /></p>
    `;
}

async function lahee_request(request, success, failure) {
    try {
        console.log("Requesting: " + request);
        var resp = await fetch(LAHEE_URL, {
            body: request,
            method: "POST"
        });

        if (!resp.ok) {
            throw new Error("Network request failed: " + resp.status);
        }

        var data = await resp.json();
        console.log(data);

        if (success) {
            success(data);
        }
    } catch (e) {
        console.error(e);
        if (failure) {
            failure(e);
        }
    }
}

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

function lahee_postinit(res) {

    try {
        if (res.users.length == 0) {
            document.getElementById("page_loading").innerHTML = "No user data is registered on LAHEE. Connect your emulator and create user data before attempting to use the web UI.";
            return;
        }
        if (res.games.length == 0) {
            document.getElementById("page_loading").innerHTML = "No game data is registered on LAHEE. Register games and achievements first before attempting to use the web UI. See readme for more information.";
            return;
        }

        lahee_data = res;

        lahee_data.users.sort(function (a, b) {
            return a.UserName.localeCompare(b.UserName);
        });
        lahee_data.games.sort(function (a, b) {
            return a.Title.localeCompare(b.Title);
        });

        document.getElementById("lahee_version").innerText = res.version;

        var users = "";
        for (var user of res.users) {
            users += "<option value='" + user.ID + "'>" + user.UserName + "</option>";
        }
        document.getElementById("user_select").innerHTML = users;

        var games = "";
        for (var game of res.games) {
            games += "<option value='" + game.ID + "'>" + game.Title + "</option>";
        }

        lahee_connect_liveticker();
        setTimeout(function () {
            try {
                document.getElementById("game_select").innerHTML = games;
                document.getElementById("main_nav").style.visibility = "visible";
                document.getElementById("main_data_selector").style.visibility = "visible";
                lahee_autoselect_based_on_most_recent_achievement();
                lahee_change_game();
                lahee_change_lb();
                lahee_set_page("page_achievements");
            } catch (e) {
                lahee_init_error(e);
            }
        }, 1000);
    } catch (e) {
        lahee_init_error(e);
    }
}

function lahee_update_game_status(ping_type) {
    lahee_request("r=laheeuserinfo&user=" + lahee_user.UserName + "&gameid=" + lahee_game.ID, function (res) {
        var msg;
        if (res.currentgameid == lahee_game.ID) {
            var lastplay = new Date() - new Date(res.lastping ?? res.lastplay);
            if (lastplay < 600_000) {
                msg = res.gamestatus + "\nPlaytime: " + TimeSpan.parse(res.playtime).toStringWithoutMs();
            } else {
                msg = "Game last played: " + TimeSpan.fromMilliseconds(lastplay).toShortString() + " ago";
            }
        } else {
            msg = "Not currently playing " + lahee_game.Title + ".";
        }
        document.getElementById("ingame").innerText = msg;

        if (!lahee_user.GameData[lahee_game.ID]) {
            lahee_user.GameData[lahee_game.ID] = {};
        }
        lahee_user.GameData[lahee_game.ID].Achievements = res.achievements;

        if (ping_type != "Time") {
            lahee_build_achievements(lahee_user, lahee_game);
        }
    });
}

function lahee_change_game() {
    var ru = document.getElementById("user_select").value;
    var rg = document.getElementById("game_select").value;

    var user = (lahee_data.users.filter(u => u.ID == ru) ?? [])[0];
    var game = (lahee_data.games.filter(g => g.ID == rg) ?? [])[0];

    if (!user || !game) {
        console.error("Can't switch to undefined user/game: " + user?.ID + "," + game?.ID);
        return;
    }

    if (!user.AllowUse) {
        alert("An error occurred while trying to load save data for this user. Check LAHEE log files.");
        return;
    }

    var prev_user_id = lahee_user?.ID;

    lahee_user = user;
    lahee_game = game;

    document.getElementById("useravatar").src = "../UserPic/" + user.UserName + ".png";
    document.getElementById("gameavatar").src = game.ImageIconURL;

    lahee_build_game_selector(user);
    lahee_build_achievements(user, game);
    lahee_build_leaderboards(user, game);
    lahee_change_lb();
    lahee_update_game_status();
    if (prev_user_id != user.ID) {
        console.log("User changed (or first load), updating stats");
        lahee_create_stats(user);
    }
}

function lahee_autoselect_based_on_most_recent_achievement() {
    var last_user = null;
    var last_game = null;
    var last_time = 0;
    for (var u of lahee_data.users) {
        for (var gid of Object.keys(u.GameData)) {
            var g = u.GameData[gid];
            for (var aid of Object.keys(g.Achievements)) {
                var a = g.Achievements[aid];
                var time = Math.max(a.AchieveDateSoftcore, a.AchieveDate);
                if (time > last_time) {
                    last_user = u.ID;
                    last_game = gid;
                    last_time = time;
                }
            }
        }
    }

    if (last_user != null && last_game != null) {
        document.getElementById("user_select").value = last_user;
        document.getElementById("game_select").value = last_game;
        console.log("Latest Achievement was from " + last_time + " from UID " + last_user + " in " + last_game + "(" + lahee_data.users[last_user]?.GameData[last_game]?.Title + ")");
    }
}

function lahee_build_achievements(user, game) {

    var ug = user.GameData[game.ID] ?? {};

    var content = "<div class='ach_grid'>";

    var sort = document.getElementById("sort_select").value;
    var filter = document.getElementById("filter").value;
    var arr = game.Achievements.slice();

    if (filter) {
        filter = filter.toLowerCase();
        arr = arr.filter(a => {
            return a.Title.toLowerCase().includes(filter) ||
                a.Description.toLowerCase().includes(filter);
        });
    }

    arr.sort(function (a, b) {
        var ua = (ug?.Achievements ?? [])[a.ID] ?? {};
        if (sort == 1) {
            if (!ua.AchieveDate && !ua.AchieveDateSoftcore) {
                return 0;
            }
            return 1;
        } else if (sort == 2) {
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 0;
            }
            return 1;
        } else if (sort == 3) {
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 1;
            }
            if (a.Type == "missable") {
                return -1;
            }
            return 1;
        } else if (sort == 4) {
            if (ua.AchieveDate || ua.AchieveDateSoftcore) {
                return 1;
            }
            if (ug?.FlaggedAchievements.includes(a.ID)) {
                return -1;
            }
            return 1;
        } else if (sort == 5) {
            return b.Points - a.Points;
        }
    });

    var pt = 0;
    var maxpt = 0;

    for (var a of arr) {
        var ua = user.GameData[game.ID]?.Achievements[a.ID] ?? {};

        var status = ua.Status ?? 0;

        if (status > 0) {
            pt += a.Points;
        }
        maxpt += a.Points;

        content += lahee_render_achievement(game, ug, a, ua);
    }

    if (tooltipList) {
        for (var t of tooltipList) {
            t.dispose();
        }
    }

    document.getElementById("achievementgrid").innerHTML = content + "</div>";

    var gamept = document.getElementById("gameptprogress");
    gamept.style.width = (pt / maxpt * 100) + "%";
    gamept.innerText = Math.floor(pt / maxpt * 100) + "%";
    document.getElementById("gamept").innerText = pt.toLocaleString() + "/" + maxpt.toLocaleString();

    var totalpt = 0;
    for (var g of lahee_data.games) {
        for (var a of g.Achievements) {
            var ua = user.GameData[g.ID]?.Achievements[a.ID] ?? {};
            if (ua.Status > 0) {
                totalpt += a.Points;
            }
        }
    }
    document.getElementById("totalpt").innerText = totalpt.toLocaleString();
    document.getElementById("adetail_info").style.display = "block";

    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl, {
        trigger: 'hover'
    }));
}

function lahee_select_ach(gid, aid) {
    var ach = ((lahee_data.games.filter(g => g.ID == gid) ?? [])[0]?.Achievements.filter(a => a.ID == aid) ?? [])[0];

    if (!ach) {
        console.error("AID not found: " + aid);
        return;
    }

    var game = lahee_get_game_by_achievement(ach.ID);

    lahee_achievement = aid;

    var ugd = lahee_user.GameData[gid];
    var ua = lahee_user.GameData[gid]?.Achievements[aid] ?? {};

    document.getElementById("adetail_img").innerHTML = lahee_render_achievement(game, ugd, ach, ua);
    document.getElementById("adetail_title").innerText = ach.Title;
    document.getElementById("adetail_desc").innerText = ach.Description;
    var type = ach.Type ?? "";
    if ((ach.Flags & 4) != 0) {
        type += " (Unofficial)";
    }
    document.getElementById("adetail_type").innerText = type;
    document.getElementById("adetail_score").innerText = ach.Points;

    var status = "Locked";
    var unlockDate = "Locked";
    var unlockTime = "Locked";
    if (ua.Status == 2) {
        status = "Hardcore Unlocked";
        unlockDate = Intl.DateTimeFormat(undefined, {
            dateStyle: 'short',
            timeStyle: 'short'
        }).format(new Date(ua.AchieveDate * 1000));
        unlockTime = TimeSpan.parse(ua.AchievePlaytime).toStringWithoutMs();
    } else if (ua.Status == 1) {
        status = "Unlocked";
        unlockDate = Intl.DateTimeFormat(undefined, {
            dateStyle: 'short',
            timeStyle: 'short'
        }).format(new Date(ua.AchieveDateSoftcore * 1000));
        unlockTime = TimeSpan.parse(ua.AchievePlaytimeSoftcore).toStringWithoutMs();
    }

    document.getElementById("adetail_status").innerText = status;
    document.getElementById("adetail_unlock").innerText = unlockDate;
    document.getElementById("adetail_unlock_pt").innerText = unlockTime;
    document.getElementById("comment_controls").style.display = "block";

    if (gid != lahee_game.ID) {
        document.getElementById("game_select").value = gid;
        lahee_change_game();
    }
    lahee_set_page('page_achievements');
    lahee_load_comments(aid);
}

function lahee_connect_liveticker() {
    var socket = new WebSocket("ws://" + (window.location.hostname != "" ? window.location.hostname : "localhost") + ":8001");

    socket.onopen = function () {
        console.log("[LiveTicker] Connection established");
        document.getElementById("ingame").innerText = "LiveTicker: Connected. Waiting for game start.";
    };

    socket.onmessage = function (event) {
        console.log("[LiveTicker] Data received from server: " + event.data);
        lahee_liveticker_update(JSON.parse(event.data));
    };

    socket.onclose = function (event) {
        if (event.wasClean) {
            console.log("[LiveTicker] Connection closed cleanly, code=" + event.code + " reason=" + event.reason);
        } else {
            console.log("[LiveTicker] Connection died");
        }
        document.getElementById("ingame").innerText = "LiveTicker: Reconnecting...";
        setTimeout(lahee_connect_liveticker, 5000);
    };

    socket.onerror = function (error) {
        console.log("[LiveTicker] " + error);
    };
}

function lahee_liveticker_update(data) {
    if (data.type == "ping") {
        lahee_update_game_status(data.pingType);
    } else if (data.type == "unlock") {
        lahee_play_unlock_sound(data.gameId, data.userAchievementData);
    } else {
        console.warn("unknown event: " + data.type);
    }
}

function lahee_build_leaderboards(user, game) {
    var list = "";

    var has_leaderboards = game.Leaderboards && game.Leaderboards.length != 0;

    if (has_leaderboards) {
        for (var le of game.Leaderboards) {
            list += "<option value='" + le.ID + "'>" + le.Title + "</option>";
        }
    } else {
        list += "<option value=''>No entries exist for this game.</option>";
    }

    var lb = document.getElementById("lb_id");
    lb.innerHTML = list;
    lb.disabled = !has_leaderboards;

    var lt = document.getElementById("lb_table");
    lt.style.display = has_leaderboards ? "table" : "none";
}

function lahee_change_lb() {

    var lb_id = document.getElementById("lb_id").value;

    var ul = (lahee_user?.GameData[lahee_game.ID]?.LeaderboardEntries ?? [])[lb_id] ?? [];
    var gl = (lahee_game?.Leaderboards?.filter(glb => glb.ID == lb_id) ?? [])[0];
    if (!gl) {
        console.error("leaderboard id not found: " + lb_id);
        return;
    }

    var sort = document.getElementById("lbsort_select").value;
    var arr = ul.slice();
    for (var i = 0; i < arr.length; i++) {
        arr[i]._presort_index = i;
    }
    arr.sort(function (a, b) {
        if (sort == 0) {
            return b.Score - a.Score;
        } else if (sort == 1) {
            return b.RecordDate - a.RecordDate;
        }
    });

    var list = "";

    if (ul.length > 0) {
        var format = Intl.DateTimeFormat(undefined, {dateStyle: 'short', timeStyle: 'short'});
        for (var e of arr) {
            list += `
            <tr>
                <td>${e._presort_index + 1}</td>
                <td>${e.Score.toLocaleString()}</td>
                <td>${format.format(new Date(e.RecordDate * 1000))}</td>
                <td>${TimeSpan.parse(e.PlayTime).toStringWithoutMs()}</td>
            </tr>
            `;
        }
    } else {
        list = "<tr><td colspan='4'>No scores recorded.</td></tr>";
    }

    document.getElementById("lb_content").innerHTML = list;
    document.getElementById("lb_desc").innerHTML = gl.Description;
}

function lahee_play_unlock_sound(gid, uad) {
    var ach = (lahee_game.Achievements.filter(a => a.ID == uad.AchievementID) ?? [])[0];

    if (ach) {
        lahee_select_ach(gid, ach.ID);

        if (Notification.permission == "granted") {
            new Notification("Achievement Unlocked!", {body: ach.Title + " (" + ach.Points + ")", icon: ach.BadgeURL});
            lahee_audio_play("162482__kastenfrosch__achievement.mp3");
        }
    }
}

function lahee_audio_play(audio) {
    if (Date.now() < lahee_last_audio + 1000) {
        console.log("not playing audio, too close to previous audio");
        return;
    }
    try {
        var sound = new Audio("sounds/" + audio);
        sound.play();
    } catch (e) {
        console.warn("Failed playing audio", e);
    }
    lahee_last_audio = Date.now();
}

function lahee_load_comments(aid) {
    var str = "";

    if (lahee_data.comments) {
        comments = lahee_data.comments.sort(function (a, b) {
            return new Date(b.Submitted) - new Date(a.Submitted);
        });
        for (var c of comments) {
            if (c.AchievementID == aid) {
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
    lahee_request("r=laheewritecomment&user=" + lahee_user.UserName + "&gameid=" + lahee_game.ID + "&aid=" + lahee_achievement + "&comment=" + encodeURIComponent(document.getElementById("comment_body").value), function (ret) {
        if (ret.Success) {
            lahee_data.comments = ret.Comments;
            lahee_load_comments(lahee_achievement);
            lahee_popup.hide();
        } else {
            throw new Error(ret.Error);
        }
    }, function (e) {
        alert("Error occurred while adding comment: " + e);
    });
}

function lahee_download_comments() {
    document.getElementById("ra_download_btn").disabled = true;
    lahee_request("r=laheefetchcomments&user=" + lahee_user.UserName + "&gameid=" + lahee_game.ID + "&aid=" + lahee_achievement, function (ret) {
        document.getElementById("ra_download_btn").disabled = false;
        if (ret.Success) {
            lahee_data.comments = ret.Comments;
            lahee_load_comments(lahee_achievement);
        } else {
            throw new Error(ret.Error);
        }
    }, function (e) {
        document.getElementById("ra_download_btn").disabled = false;
        alert("Error occurred while downloading RA data: " + e);
    });
}

// noinspection JSUnusedGlobalSymbols
function lahee_delete_comment(id) {
    if (confirm("Are you sure that you want to delete a comment?")) {
        lahee_request("r=laheedeletecomment&uuid=" + id + "&gameid=" + lahee_game.ID, function (ret) {
            if (ret.Success) {
                lahee_data.comments = ret.Comments;
                lahee_load_comments(lahee_achievement);
            } else {
                throw new Error(ret.Error);
            }
        }, function (e) {
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
    lahee_request("r=laheeflagimportant&user=" + lahee_user.UserName + "&gameid=" + lahee_game.ID + "&aid=" + lahee_achievement, function (ret) {
        if (ret.Success) {
            lahee_user.GameData[lahee_game.ID].FlaggedAchievements = ret.Flagged;

            lahee_build_achievements(lahee_user, lahee_game);
            lahee_select_ach(lahee_game.ID, lahee_achievement);
        } else {
            throw new Error(ret.Error);
        }
    }, function (e) {
        alert("Error communicating with LAHEE: " + e);
    });
}

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

    for (var ug of Object.values(user.GameData)) {
        var game = (lahee_data.games.filter(g => g.ID == ug.GameID) ?? [])[0];
        console.log("Checking: " + game?.Title + "(" + ug.GameID + ")");
        var user_achievement_arr = Object.values(ug.Achievements);

        var total_achievements = game?.Achievements.length ?? -1;
        var hardcore_achievements = user_achievement_arr.filter(a => a.Status == 2).length;
        var softcore_achievements = user_achievement_arr.filter(a => a.Status == 1).length;
        var completion_ids = game?.Achievements.filter(a => a.Type == "win_condition").map(a => a.ID) ?? [];

        var playtime_ms = 0;
        if (ug.PlayTimeApprox) {
            playtime_ms = TimeSpan.parse(ug.PlayTimeApprox).valueOf();
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
                var at = TimeSpan.parse(ua.Status == 2 ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore);
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
            for (var a of game.Achievements) {
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
                    <td>${new Date(ug.FirstPlay).toLocaleString()}</td>
                    <td>${TimeSpan.parse(ug.PlayTimeApprox).toStringWithoutMs()}</td>
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

function lahee_stats_render_game(suffix, user, gameid, time) {
    var game = (lahee_data.games.filter(g => g.ID == gameid) ?? [])[0];
    var ug = user?.GameData[gameid];

    var total_achievements = game?.Achievements.length ?? -1;
    var achievements_softcore = game && ug ? Object.values(ug.Achievements).filter(a => a.Status == 1).reduce((partialSum, a) => partialSum + a, 0) : -2;
    var achievements_hardcore = game && ug ? Object.values(ug.Achievements).filter(a => a.Status == 2).reduce((partialSum, a) => partialSum + a, 0) : -2;
    var status = 0;
    if (total_achievements == achievements_hardcore) {
        status = 2;
    } else if (total_achievements == achievements_softcore) {
        status = 1;
    }

    var img = document.getElementById("game_img_" + suffix);
    for (var i = 0; i < 4; i++) {
        img.classList.remove("ach_status_" + i);
    }
    img.classList.add("ach_status_" + status);
    img.src = game ? game.ImageIconURL : "";
    document.getElementById("game_title_" + suffix).innerText = game ? game.Title : (gameid > 0 ? "Unknown Game: " + gameid : "No Data");
    document.getElementById("game_time_" + suffix).innerText = time ? TimeSpan.fromMilliseconds(time).toStringWithoutMs() : (ug ? TimeSpan.parse(ug.PlayTimeApprox).toStringWithoutMs() : "--:--:--");
}

function lahee_get_achievement(aid) {
    for (var g of lahee_data.games) {
        for (var a of g.Achievements) {
            if (a.ID == aid) {
                return a;
            }
        }
    }
    return null;
}

function lahee_get_game_by_achievement(aid) {
    for (var g of lahee_data.games) {
        for (var a of g.Achievements) {
            if (a.ID == aid) {
                return g;
            }
        }
    }
    return null;
}

function lahee_stats_render_milestones(user) {
    var all_user_achievements = Object.values(user.GameData).flatMap((ug) => Object.values(ug.Achievements)).filter(a => a.Status != 0).sort(function (a, b) {
        return Math.max(a.AchieveDate, a.AchieveDateSoftcore) - Math.max(b.AchieveDate, b.AchieveDateSoftcore);
    });
    var milestone_html = "";

    var milestones = [1, 2, 5, 10, 15, 20, 25, 50, 100, 123, 150, 200, 250, 300, 350, 400, 450, 500, 600, 666, 700, 777, 800, 900, 1000, 1234, 1337, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 6000, 6666, 7000, 7777, 8000, 9000, 10000];
    for (var i of milestones) {
        if (all_user_achievements[i - 1]) {
            var ua = all_user_achievements[i - 1];

            var game = lahee_get_game_by_achievement(ua.AchievementID);
            var ach = (game?.Achievements.filter(a => a.ID == ua.AchievementID) ?? [])[0];

            milestone_html += `
                <tr>
                    <td><img src="${game?.ImageIconURL}" height="64" /></td>
                    <td>${lahee_render_achievement(game, ua, ach, ua)}</td>
                    <td>${ach?.Title ?? ("Unknown Achievement: " + ua.AchievementID)}<br /><small>${game?.Title ?? "Unknown Game"}</small></td>
                    <td>${new Date(Math.max(ua.AchieveDate, ua.AchieveDateSoftcore) * 1000).toLocaleString()}</td>
                    <td>${TimeSpan.parse(ua.AchieveDate != 0 ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).toStringWithoutMs()}</td>
                    <td>#${i}</td>
                </tr>
            `;
        }
    }
    document.getElementById("milestones_table").innerHTML = milestone_html;
}

// object returned from any of the lahee_check_meta_* functions
class LaheeMetaResult {
    // the achievement that fulfills this meta-achievement
    user_achievement;
    // the name of the meta-achievement
    name;
    // explanation of the meta-achievement
    description;
    // an array of achievements related to fulfilling the condition
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
            var game = lahee_get_game_by_achievement(ua.AchievementID);
            var ug = lahee_user.GameData[game?.ID];
            var ach = (game?.Achievements.filter(a => a.ID == ua.AchievementID) ?? [])[0];

            var related_html = "";
            for (var related_ua of meta_data.related_user_achievements ?? []) {
                var related_ach = (game?.Achievements.filter(a => a.ID == related_ua.AchievementID) ?? [])[0];
                related_html += lahee_render_achievement(game, ug, related_ach, ua);
            }

            meta_html += `
                <tr>
                    <td>${meta_data.name}<br /><small>${meta_data.description}</small></td>
                    <td><img src="${game?.ImageIconURL}" height="64" /></td>
                    <td>${lahee_render_achievement(game, ug, ach, ua)}</td>
                    <td>${ach?.Title ?? ("Unknown Achievement: " + ua.AchievementID)}<br /><small>${game?.Title ?? "Unknown Game"}</small></td>
                    <td>${related_html}</td>
                    <td>${new Date(Math.max(ua.AchieveDate, ua.AchieveDateSoftcore) * 1000).toLocaleString()}</td>
                    <td>${TimeSpan.parse(ua.AchieveDate != 0 ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).toStringWithoutMs()}</td>
                </tr>
            `;
        }
    }

    document.getElementById("meta_table").innerHTML = meta_html;
}

function lahee_check_meta_first(user) {
    var all_user_achievements_sorted = Object.values(user.GameData).flatMap((ug) => Object.values(ug.Achievements)).filter(a => a.Status != 0).sort(function (a, b) {
        return Math.max(a.AchieveDate, a.AchieveDateSoftcore) - Math.max(b.AchieveDate, b.AchieveDateSoftcore);
    });

    if (all_user_achievements_sorted[0]) {
        return new LaheeMetaResult(all_user_achievements_sorted[0], "The First", "The very first achievement you have obtained.");
    }

    return null;
}

function lahee_check_meta_nut(user) {
    var all_user_achievements = Object.values(user.GameData).flatMap((ug) => Object.values(ug.Achievements));

    if (all_user_achievements.length < 2) { // condition can't be met if we have less than 2 achievements
        return null;
    }

    var highest_diff = 0; // currently highest time difference between two achievements
    var highest_diff_previous_ua = null; // achievement before our current top time difference candidate
    var highest_diff_ua = null; // current highest time difference achievement

    for (const is_hardcore of [false, true]) {

        var previous_ua = null;
        var all_user_achievements_sorted = all_user_achievements.slice().sort(function (a, b) {
            return is_hardcore ? a.AchieveDate - b.AchieveDate : a.AchieveDateSoftcore - b.AchieveDateSoftcore;
        });

        for (var ua of all_user_achievements_sorted) {

            var playtime = TimeSpan.parse(is_hardcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).valueOf();
            if (playtime <= 0) {
                continue;
            }

            var previous_playtime = previous_ua ? TimeSpan.parse(is_hardcore ? previous_ua.AchievePlaytime : previous_ua.AchievePlaytimeSoftcore).valueOf() : 0;

            var diff = Math.abs(playtime - previous_playtime);
            if (diff > highest_diff) {
                var game1 = lahee_get_game_by_achievement(ua.AchievementID);
                var game2 = lahee_get_game_by_achievement(previous_ua?.AchievementID);
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
        var ach = lahee_get_achievement(highest_diff_previous_ua.AchievementID);
        return new LaheeMetaResult(highest_diff_ua, "The Nut", "The achievement with the longest time between the previous achievement and this one.<br>(You: " + TimeSpan.fromMilliseconds(highest_diff).toStringWithoutMs() + " since " + (ach.Title ?? "Unknown Achievement: " + highest_diff_previous_ua.AchievementID) + ")", [highest_diff_previous_ua]);
    }

    return null;
}

function lahee_check_meta_grind(user) {
    var all_user_achievements_sorted = Object.values(user.GameData).flatMap((ug) => Object.values(ug.Achievements)).sort(function (a, b) {
        return Math.max(a.AchieveDate, a.AchieveDateSoftcore) - Math.max(b.AchieveDate, b.AchieveDateSoftcore);
    });

    var current_highest_points = 0;
    var current_highest_playtime = 0;
    var current_highest_ua = null;

    for (var ua of all_user_achievements_sorted) {
        var game = lahee_get_game_by_achievement(ua.AchievementID);
        var ach = (game?.Achievements.filter(a => a.ID == ua.AchievementID) ?? [])[0];

        var playtime = TimeSpan.parse(ua.AchieveDate != 0 ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).valueOf();

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
    var all_user_achievements = Object.values(user.GameData).flatMap((ug) => Object.values(ug.Achievements));

    if (all_user_achievements.length < 2) { // condition can't be met if we have less than 2 achievements
        return null;
    }

    var highest_combo_count = 1; // combo needs to be longer than 1
    var highest_combo_ua = []; // achievements of current best combo
    var highest_combo_time = 0; // time length how long the achievement combo was held

    for (const is_hardcore of [false, true]) {

        var all_user_achievements_sorted = all_user_achievements.sort(function (a, b) {
            return is_hardcore ? a.AchievePlaytime - b.AchievePlaytime : a.AchievePlaytimeSoftcore - b.AchievePlaytimeSoftcore;
        });

        var current_combo_count = 0;
        var current_combo_ua = [];
        var current_combo_diff_total = 0;
        var previous_ua = null;

        for (var ua of all_user_achievements_sorted) {

            var playtime = TimeSpan.parse(is_hardcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore).valueOf();
            if (playtime <= 0) {
                continue;
            }

            var previous_playtime = previous_ua ? TimeSpan.parse(is_hardcore ? previous_ua.AchievePlaytime : previous_ua.AchievePlaytimeSoftcore).valueOf() : 0;

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
        return new LaheeMetaResult(highest_combo_ua[0], "The Combo", "The longest combo of achievements within 10 minutes of each other.<br>(You: " + highest_combo_count + " within " + TimeSpan.fromMilliseconds(highest_combo_time).toStringWithoutMs() + ")", highest_combo_ua);
    }

    return null;
}

function lahee_render_achievement(game, ug, a, ua, size) {
    var status = ua.Status ?? 0;
    if (!a) {
        a = {
            Title: "Unknown Achievement: " + ua.AchievementID,
            Description: "Unknown Achievement",
            Flags: 0,
            Type: "",
            BadgeURL: "/Badge/00000.png",
            BadgeLockedURL: "/Badge/00000.png"
        };
    }
    if (!game) {
        game = {
            ID: 0
        };
    }
    return `<img src="${status != 0 ? a.BadgeURL : a.BadgeLockedURL}" class="ach ach_type_${a.Type} ach_status_${status} ach_flags_${a.Flags} ${ug?.FlaggedAchievements?.includes(a.ID) ? "ach_flag_important" : ""}" onclick="lahee_select_ach(${game.ID}, ${a.ID});" loading="lazy" data-bs-html="true" data-bs-toggle="tooltip" data-bs-title="<b>${a.Title.replaceAll("\"", "&quot;")}</b> (${a.Points})<hr />${a.Description.replaceAll("\"", "&quot;")}" ${size ? "width='" + size + "'" : ""} />`;
}

function lahee_show_code_popup() {
    var ach = lahee_get_achievement(lahee_achievement);
    var game = lahee_get_game_by_achievement(lahee_achievement);
    if (!ach || !game) {
        return;
    }

    var ug = lahee_user.GameData[game.ID] ?? {};
    var ua = (ug?.Achievements ?? [])[ach.ID] ?? {};

    if (!ach.MemAddr) {
        alert("This achievement has no code.");
        return;
    }

    document.getElementById("download_ach_code_btn").disabled = true;
    lahee_request("r=laheeachievementcode&gameid=" + game.ID + "&aid=" + lahee_achievement, function (ret) {
        if (ret.Success) {
            lahee_update_code_popup(game, ug, ach, ua, ret.CodeNotes, ret.TriggerGroups);
            lahee_popup = new bootstrap.Modal(document.getElementById('codeModal'), {});
            lahee_popup.show();
            document.getElementById("download_ach_code_btn").disabled = false;
        } else {
            throw new Error(ret.Error);
        }
    }, function (e) {
        document.getElementById("download_ach_code_btn").disabled = false;
        alert("Error occurred while loading data: " + e);
    });
}

function lahee_update_code_popup(game, ug, a, ua, codenotes, groups) {
    var html = "";

    for (var i = 0; i < groups.length; i++) {
        var group = groups[i];
        var ht = i == 0 ? "All the conditions in the core group must be true" : "When using Alt groups, for the achievement to trigger, all the conditions in the Core group MUST be true.&#013;And then all the conditions of ANY Alt group must be true.&#013;In other words, each Alt group uses OR logic.";
        html += "<tr><td colspan='7' class='text-center fw-bold'><span class='hoverable_text' title='" + ht + "'>Group " + (i + 1) + " (" + (i == 0 ? "Core" : "Alt " + i) + ")</span></td></tr>";

        for (var req of group.Requirements) {

            html += `<tr>
                <td><span class="hoverable_text" title="${lahee_trigger_parse_type_desc(req.Type)}">${lahee_trigger_parse_type(req.Type)}</span></td>
                <td>
                    <span class="hoverable_text" title="${lahee_trigger_field_parse_type_desc(req.Left.Type)}">${lahee_trigger_field_parse_type(req.Left.Type)}</span>
                    <span class="hoverable_text" title="${lahee_trigger_field_parse_size_desc(req.Left.Size)}">${lahee_trigger_field_parse_size(req.Left.Size)}</span>
                </td>
                <td>${lahee_trigger_render_value(codenotes, req.Left)}</td>
                <td>${lahee_trigger_get_operator(req.Operator)}</td>
                <td>
                    <span class="hoverable_text" title="${lahee_trigger_field_parse_type_desc(req.Right.Type)}">${lahee_trigger_field_parse_type(req.Right.Type)}</span>
                    <span class="hoverable_text" title="${lahee_trigger_field_parse_size_desc(req.Right.Size)}">${lahee_trigger_field_parse_size(req.Right.Size)}</span>
                </td>
                <td>${lahee_trigger_render_value(codenotes, req.Right)}</td>
                <td>${req.HitCount}</td>
            </tr>`;
        }

    }

    document.getElementById("codeModalTitle").innerHTML = lahee_render_achievement(game, ug, a, ua, 32) + " " + a.Title + ": Achievement Code";
    document.getElementById("ach_code_desc").innerHTML = a.Description;
    document.getElementById("ach_code_table").innerHTML = html;
}

function lahee_trigger_parse_type(t) {
    switch (t) {
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
            return "Unknown: " + t;
    }
}

function lahee_trigger_parse_type_desc(t) {
    switch (t) {
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
            return "Unknown: " + t;
    }
}

function lahee_trigger_get_operator(o) {
    switch (o) {
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
            return "Unknown: " + t;
    }
}

function lahee_trigger_field_parse_type(o) {
    switch (o) {
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
            return "Unknown: " + o;
    }
}

function lahee_trigger_field_parse_type_desc(o) {
    switch (o) {
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
            return "Unknown: " + o;
    }
}

function lahee_trigger_field_parse_size(s) {
    switch (s) {
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
            return "Unknown: " + s;
    }
}

function lahee_trigger_field_parse_size_desc(s) {
    switch (s) {
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
            return "Unknown: " + s;
    }
}

function lahee_find_code_note_text(codenotes, addr) {
    if (!codenotes) {
        return null;
    }
    for (var cn of codenotes) {
        if (Number(cn.Address) == addr) {
            return cn.Note;
        }
    }
    return null;
}

function lahee_trigger_render_value(codenotes, field) {
    var cn = null;
    if (field.IsMemoryReference) {
        cn = lahee_find_code_note_text(codenotes, field.Value);
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

function lahee_build_game_selector(user) {
    if (!user) {
        var ru = document.getElementById("user_select").value;
        user = (lahee_data.users.filter(u => u.ID == ru) ?? [])[0];
    }
    var now = new Date();
    var gamelist = lahee_data.games.slice();

    var games = "<optgroup label='Recently Played'>";
    for (var game of gamelist) {
        var ug = user.GameData[game.ID] ?? {};
        if (now - new Date(ug.LastPlay) < 30 * 24 * 60 * 60 * 1000) {
            games += "<option value='" + game.ID + "' " + (lahee_game.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
            gamelist = gamelist.filter(g => g.ID != game.ID);
        }
    }
    games += "</optgroup>";

    games += "<optgroup label='Unplayed'>";
    for (var game of gamelist) {
        var ug = user.GameData[game.ID] ?? {};
        if (!ug.Achievements || Object.keys(ug.Achievements).length == 0) {
            games += "<option value='" + game.ID + "' " + (lahee_game.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
            gamelist = gamelist.filter(g => g.ID != game.ID);
        }
    }
    games += "</optgroup>";

    // calculate beaten/completed games first, so unfinished doesn't take priority
    var games_completed = "<optgroup label='Completed'>";
    for (var game of gamelist) {
        var ug = user.GameData[game.ID] ?? {};
        if (ug.Achievements && Object.keys(ug.Achievements).length >= game.Achievements.length) {
            games_completed += "<option value='" + game.ID + "' " + (lahee_game.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
            gamelist = gamelist.filter(g => g.ID != game.ID);
        }
    }
    games_completed += "</optgroup>";

    var games_beaten = "<optgroup label='Beaten'>";
    for (var game of gamelist) {
        var completion_ids = game?.Achievements.filter(a => a.Type == "win_condition").map(a => a.ID) ?? [];
        var ug = user.GameData[game.ID] ?? {};
        if (Object.values(ug.Achievements).filter(a => completion_ids.includes(a.AchievementID) && a.Status > 0).length > 0) {
            games_beaten += "<option value='" + game.ID + "' " + (lahee_game.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
            gamelist = gamelist.filter(g => g.ID != game.ID);
        }
    }
    games_beaten += "</optgroup>";

    games += "<optgroup label='Unfinished'>";
    for (var game of gamelist) {
        var ug = user.GameData[game.ID] ?? {};
        if (!ug.Achievements || Object.keys(ug.Achievements).length < game.Achievements.length) {
            games += "<option value='" + game.ID + "' " + (lahee_game.ID == game.ID ? "selected" : "") + ">" + game.Title + "</option>";
            gamelist = gamelist.filter(g => g.ID != game.ID);
        }
    }
    games += "</optgroup>";

    games += games_beaten;
    games += games_completed;

    document.getElementById("game_select").innerHTML = games;
}