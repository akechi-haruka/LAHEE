// noinspection EqualityComparisonWithCoercionJS,JSUnresolvedReference

var LAHEE_URL;
var lahee_data;
var lahee_user;
var lahee_game;
var lahee_last_audio = 0;
var lahee_popup;
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
            var lastping = new Date() - new Date(res.lastping);
            if (lastping < 600_000) {
                msg = res.gamestatus + "\nPlaytime: " + TimeSpan.parse(res.playtime).toStringWithoutMs();
            } else {
                msg = "Game last played: " + TimeSpan.fromMilliseconds(lastping).toShortString() + " ago";
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

    var user = null;
    for (var u of lahee_data.users) {
        if (u.ID == ru) {
            user = u;
            break;
        }
    }
    var game = null;
    for (var g of lahee_data.games) {
        if (g.ID == rg) {
            game = g;
            break;
        }
    }

    if (!user || !game) {
        console.error("Can't switch to undefined user/game: " + user?.ID + "," + game?.ID);
        return;
    }

    if (!user.AllowUse) {
        alert("An error occurred while trying to load save data for this user. Check LAHEE log files.");
        return;
    }

    lahee_user = user;
    lahee_game = game;

    document.getElementById("useravatar").src = "../UserPic/" + user.UserName + ".png";
    document.getElementById("gameavatar").src = game.ImageIconURL;

    lahee_build_achievements(user, game);
    lahee_build_leaderboards(user, game);
    lahee_change_lb();
    lahee_update_game_status();
    lahee_create_stats(user);
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

        content += `<img src="${status != 0 ? a.BadgeURL : a.BadgeLockedURL}" class="ach_type_${a.Type} ach_status_${status} ${ug.FlaggedAchievements?.includes(a.ID) ? "ach_flag_important" : ""}" onclick="lahee_select_ach(${game.ID}, ${a.ID});" loading="lazy" data-bs-html="true" data-bs-toggle="tooltip" data-bs-title="<b>${a.Title.replaceAll("\"", "&quot;")}</b> (${a.Points})<hr />${a.Description.replaceAll("\"", "&quot;")}" />`;
    }
    
    if (tooltipList){
        for (var t of tooltipList){
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
    var ach;
    for (var a of lahee_game.Achievements) {
        if (a.ID == aid) {
            ach = a;
            break;
        }
    }
    if (!ach) {
        console.error("AID not found: " + aid);
        return;
    }

    lahee_achievement = aid;

    var ugd = lahee_user.GameData[gid];
    var ua = lahee_user.GameData[gid]?.Achievements[aid] ?? {};

    var img = document.getElementById("adetail_img");
    img.src = ua.Status ? a.BadgeURL : a.BadgeLockedURL;
    img.classList.remove("ach_type_missable");
    img.classList.remove("ach_type_progression");
    img.classList.remove("ach_type_win_condition");
    img.classList.remove("ach_flag_important");
    for (var i = 0; i < 4; i++) {
        img.classList.remove("ach_status_" + i);
    }
    img.classList.add("ach_type_" + a.Type);
    img.classList.add("ach_status_" + ua.Status);
    if (ugd && ugd.FlaggedAchievements?.includes(aid)) {
        img.classList.add("ach_flag_important");
    }

    document.getElementById("adetail_title").innerText = a.Title;
    document.getElementById("adetail_desc").innerText = a.Description;
    document.getElementById("adetail_type").innerText = a.Type;
    document.getElementById("adetail_score").innerText = a.Points;

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

    lahee_load_comments(aid);
}

function lahee_connect_liveticker() {
    var socket = new WebSocket("ws://" + (window.location.hostname != "" ? window.location.hostname : "localhost") + ":8001");

    socket.onopen = function (e) {
        console.log("[open] Connection established");
        document.getElementById("ingame").innerText = "LiveTicker: Connected. Waiting for game start.";
    };

    socket.onmessage = function (event) {
        console.log(`[message] Data received from server: ${event.data}`);
        lahee_liveticker_update(JSON.parse(event.data));
    };

    socket.onclose = function (event) {
        if (event.wasClean) {
            console.log(`[close] Connection closed cleanly, code=${event.code} reason=${event.reason}`);
        } else {
            console.log('[close] Connection died');
        }
        document.getElementById("ingame").innerText = "LiveTicker: Reconnecting...";
        setTimeout(lahee_connect_liveticker, 5000);
    };

    socket.onerror = function (error) {
        console.log(`[error] ${error}`);
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
    var gl = null;

    if (lahee_game?.Leaderboards) {
        for (var glb of lahee_game.Leaderboards) {
            if (glb.ID == lb_id) {
                gl = glb;
                break;
            }
        }
    }
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
    var ach = null;
    for (var a of lahee_game.Achievements) {
        if (a.ID == uad.AchievementID) {
            ach = a;
            break;
        }
    }

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
    var shortest_pf = {id: 0, v: 9999999999999};
    var fastest_100 = {id: 0, v: 9999999999999};
    var slowest_100 = {id: 0, v: 0};
    var fastest_beat = {id: 0, v: 9999999999999};
    var slowest_beat = {id: 0, v: 0};
    var first_achievement = {id: 0, gid: 0, v: 9999999999999};
    var nut_achievement = {id: 0, gid: 0, v: 0, g: "No Data"};
    var grind_achievement = {id: 0, gid: 0, p: 0, v: 0};
    var first_play = new Date();

    game_counts[0] = lahee_data.games.length;

    for (var ug of Object.values(user.GameData)) {
        var game = null;
        for (var g of lahee_data.games) {
            if (g.ID == ug.GameID) {
                game = g;
                break;
            }
        }
        console.log("Checking: " + game?.Title + "(" + game?.ID + ")");
        var uat = Object.values(ug.Achievements);

        var total_achievements = game?.Achievements.length ?? -1;
        var hardcore_achievements = uat.filter(a => a.Status == 2).length;
        var softcore_achievements = uat.filter(a => a.Status == 1).length;
        var completion_ids = game?.Achievements.filter(a => a.Type == "win_condition").map(a => a.ID) ?? [];

        var playtime_ms = 0;
        if (ug.PlayTimeApprox) {
            playtime_ms = TimeSpan.parse(ug.PlayTimeApprox).valueOf();
        }

        if (playtime_ms > longest_pt.v) {
            longest_pt.id = ug.GameID;
            longest_pt.v = playtime_ms;
        }
        if (playtime_ms < shortest_pf.v && playtime_ms > 0) {
            shortest_pf.id = ug.GameID;
            shortest_pf.v = playtime_ms;
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
        if (uat.filter(a => completion_ids.includes(a.AchievementID) && a.Status > 0).length > 0) {
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

        var arr = Object.values(ug.Achievements).sort(function (a, b) {
            return Math.max(a.AchieveDate, a.AchieveDateSoftcore) - Math.max(b.AchieveDate, b.AchieveDateSoftcore);
        });
        var last_ach_pt = 0;
        var last_ach_name = "No Data";
        for (var ua of arr) {
            var higherPlaytime = TimeSpan.parse(ua.AchieveDate > ua.AchieveDateSoftcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore);
            if (higherPlaytime.valueOf() <= 0){
                continue;
            }
            var diff = Math.abs(higherPlaytime.valueOf() - last_ach_pt);
            if (diff > nut_achievement.v) {
                nut_achievement.id = ua.AchievementID;
                nut_achievement.gid = ug.GameID;
                nut_achievement.v = diff;
                nut_achievement.g = last_ach_name;
                last_ach_name = game?.Achievements.filter(a => a.ID == ua.AchievementID)[0]?.Title ?? "Unknown Achievement: " + ua.AchievementID;
            }
            last_ach_pt = higherPlaytime.valueOf();
        }

        if (game) {
            game_pt_total = 0;
            game_pt_hardcore = 0;
            game_pt_softcore = 0;
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

                    var earlierAchieveDate = ua.AchieveDate == 0 || ua.AchieveDateSoftcore < ua.AchieveDate ? ua.AchieveDateSoftcore : ua.AchieveDate;
                    var higherPlaytime = TimeSpan.parse(ua.AchieveDate > ua.AchieveDateSoftcore ? ua.AchievePlaytime : ua.AchievePlaytimeSoftcore);

                    if (earlierAchieveDate < first_achievement.v && earlierAchieveDate > 0) {
                        first_achievement.id = a.ID;
                        first_achievement.gid = game.ID;
                        first_achievement.v = earlierAchieveDate;
                    }
                    if (a.Points >= grind_achievement.p && higherPlaytime > grind_achievement.v) {
                        grind_achievement.id = a.ID;
                        grind_achievement.gid = game.ID;
                        grind_achievement.p = a.Points;
                        grind_achievement.v = higherPlaytime.valueOf();
                    }
                }

            }

            score_counts[0] += game_pt_total;

            table_str += `
                <tr>
                    <td><img src="${game.ImageIconURL}" height="48" /></td>
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
    lahee_stats_render_game("s", user, shortest_pf.id);
    lahee_stats_render_game("f1", user, fastest_100.id);
    lahee_stats_render_game("s1", user, slowest_100.id);
    lahee_stats_render_game("fb", user, fastest_beat.id, fastest_beat.v);
    lahee_stats_render_game("sb", user, slowest_beat.id, slowest_beat.v);
    lahee_stats_render_ach("tf", user, first_achievement.id, first_achievement.gid);
    lahee_stats_render_ach("tn", user, nut_achievement.id, nut_achievement.gid, TimeSpan.fromMilliseconds(nut_achievement.v).toStringWithoutMs() + "<br><small class='stats_small_text'>since "+nut_achievement.g+"</small>");
    lahee_stats_render_ach("tg", user, grind_achievement.id, grind_achievement.gid);

    var tt = TimeSpan.fromMilliseconds(total_time);
    document.getElementById("total_time").innerText = tt.toStringWithoutMs() + " (" + Math.floor(tt.totalHours) + "h.)";
    document.getElementById("total_counts").innerText = game_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_ach").innerText = ach_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_score").innerText = score_counts.map(n => n.toLocaleString()).join(" / ");
    document.getElementById("total_started").innerText = first_play.toLocaleDateString();
    document.getElementById("stats_table").innerHTML = table_str;
}

function lahee_stats_render_game(suffix, user, gameid, time) {
    var game = null;
    for (var g of lahee_data.games) {
        if (g.ID == gameid) {
            game = g;
            break;
        }
    }
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


function lahee_stats_render_ach(suffix, user, aid, gameid, time) {
    console.log("stats_render_ach: " + user + ", " + aid + ", " + gameid + ", " + time);
    var game = null;
    for (var g of lahee_data.games) {
        if (g.ID == gameid) {
            game = g;
            break;
        }
    }

    var a = (game?.Achievements.filter(a => a.ID == aid) ?? [])[0];
    var ua = user?.GameData[gameid]?.Achievements[aid];

    var earlydate = null;
    var earlypt = null;
    if (ua) {
        if (ua.AchieveDate != 0 && ua.AchieveDate < ua.AchieveDateSoftcore) {
            earlydate = ua.AchieveDate;
            earlypt = ua.AchievePlaytime;
        } else {
            earlydate = ua.AchieveDateSoftcore;
            earlypt = ua.AchievePlaytimeSoftcore;
        }
    }

    var img = document.getElementById("ach_img_" + suffix);
    for (var i = 0; i < 4; i++) {
        img.classList.remove("ach_status_" + i);
    }
    img.classList.add("ach_status_" + ua?.Status);
    img.src = a ? a.BadgeURL : "";
    document.getElementById("ach_title_" + suffix).innerText = a ? a.Title + " (" + a.Points + ")" : (aid > 0 ? "Unknown Achievement: " + gameid : "No Data");
    document.getElementById("ach_description_" + suffix).innerText = a ? a.Description : (aid > 0 ? "Unknown Achievement: " + gameid : "No Data");
    document.getElementById("ach_game_" + suffix).innerText = game ? game.Title : (gameid > 0 ? "Unknown Game: " + gameid : "No Data");
    document.getElementById("ach_time_" + suffix).innerHTML = time ? time : (ua ? TimeSpan.parse(earlypt).toStringWithoutMs() : "--:--:--");
    document.getElementById("ach_dtime_" + suffix).innerText = earlydate ? new Date(earlydate * 1000).toLocaleString() : "--:--:--";
}