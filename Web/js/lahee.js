var LAHEE_URL;
var lahee_data;
var lahee_user;
var lahee_game;
var lahee_last_audio = 0;

function lahee_init() {
    if (Notification.permission == "default"){
        Notification.requestPermission();
    }
    lahee_audio_play("540121__jj_om__blank-sound.ogg");
    
    LAHEE_URL = "http://"+window.location.host+"/dorequest.php";
    lahee_request("r=laheeinfo", lahee_postinit);
}

function lahee_request(request, success) {
    var r = new XMLHttpRequest();
    r.open("POST", LAHEE_URL, true);
    r.responseType = "json";
    r.onreadystatechange = function () {
        if (r.readyState != 4 || r.status != 200) { return; }
        if (success) {
            console.log(r.response);
            success(r.response);
        }
    };
    r.send(request);
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
        users += "<option value='" + user.ID +"'>"+user.UserName+"</option>";
    }
    document.getElementById("user_select").innerHTML = users;

    var games = "";
    for (var game of res.games) {
        games += "<option value='" + game.ID + "'>" + game.Title + "</option>";
    }
    document.getElementById("game_select").innerHTML = games;

    lahee_set_page("page_achievements");
    lahee_autoselect_based_on_most_recent_achievement();
    lahee_change_game();
    lahee_change_lb();
    lahee_connect_liveticker();
}

function lahee_update_game_status() {
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

        if (!lahee_user.GameData[lahee_game.ID]){
            lahee_user.GameData[lahee_game.ID] = {};
        }
        lahee_user.GameData[lahee_game.ID].Achievements = res.achievements;

        lahee_build_achievements(lahee_user, lahee_game);
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
        console.error(user + "," + game);
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
}

function lahee_autoselect_based_on_most_recent_achievement(){
    var last_user = null;
    var last_game = null;
    var last_time = 0;
    for (var u of lahee_data.users) {
        for (var gid of Object.keys(u.GameData)) {
            var g = u.GameData[gid];
            for (var aid of Object.keys(g.Achievements)){
                var a = g.Achievements[aid];
                var time = Math.max(a.AchieveDateSoftcore, a.AchieveDate);
                if (time > last_time){
                    last_user = u.ID;
                    last_game = gid;
                    last_time = time;
                }
            }
        }
    }
    
    if (last_user != null && last_game != null){
        document.getElementById("user_select").value = last_user;
        document.getElementById("game_select").value = last_game;
        console.log("Latest Achievement was from " + last_time + " from UID " + last_user + " in " + last_game);
    }
}

function lahee_build_achievements(user, game) {
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
        var ua = user.GameData[game.ID]?.Achievements[a.ID] ?? {};
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

        content += `<img src="${status != 0 ? a.BadgeURL : a.BadgeLockedURL}" class="ach_type_${a.Type} ach_status_${status}" onclick="lahee_select_ach(${game.ID}, ${a.ID});" loading="lazy" data-bs-html="true" data-bs-toggle="tooltip" data-bs-title="<b>${a.Title.replaceAll("\"", "&quot;")}</b> (${a.Points})<hr />${a.Description.replaceAll("\"", "&quot;")}" />`;
    }

    document.getElementById("achievementgrid").innerHTML = content + "</div>";

    var ug = user.GameData[game.ID] ?? {};

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
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl, {
        trigger : 'hover'
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

    var ua = lahee_user.GameData[gid]?.Achievements[aid] ?? {};

    var img = document.getElementById("adetail_img");
    img.src = ua.Status ? a.BadgeURL : a.BadgeLockedURL;
    img.classList.remove("ach_type_missable");
    for (var i = 0; i < 4; i++) {
        img.classList.remove("ach_status_" + i);
    }
    img.classList.add("ach_type_" + a.Type);
    img.classList.add("ach_status_" + ua.Status);

    document.getElementById("adetail_title").innerText = a.Title;
    document.getElementById("adetail_desc").innerText = a.Description;
    document.getElementById("adetail_type").innerText = a.Type;
    document.getElementById("adetail_score").innerText = a.Points;

    var status = "Locked";
    var unlockDate = "Locked";
    var unlockTime = "Locked";
    if (ua.Status == 2) {
        status = "Hardcore Unlocked";
        unlockDate = Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(ua.AchieveDate * 1000));
        unlockTime = TimeSpan.parse(ua.AchievePlaytime).toStringWithoutMs();
    } else if (ua.Status == 1) {
        status = "Unlocked";
        unlockDate = Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(ua.AchieveDateSoftcore * 1000));
        unlockTime = TimeSpan.parse(ua.AchievePlaytimeSoftcore).toStringWithoutMs();
    }

    document.getElementById("adetail_status").innerText = status;
    document.getElementById("adetail_unlock").innerText = unlockDate;
    document.getElementById("adetail_unlock_pt").innerText = unlockTime;
}

function lahee_connect_liveticker() {
    var socket = new WebSocket("ws://" + (window.location.hostname != "" ? window.location.hostname : "localhost")+":8001");

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

function lahee_liveticker_update(data){
    if (data.type == "ping"){
        lahee_update_game_status();
    } else if (data.type == "unlock"){
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
    lt.style.display = has_leaderboards ? "table": "none";
}

function lahee_change_lb() {

    var lb_id = document.getElementById("lb_id").value;

    var ul = (lahee_user.GameData[lahee_game.ID]?.LeaderboardEntries ?? [])[lb_id] ?? [];  
    var gl = null;

    if (lahee_game.Leaderboards) {
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
        var format = Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' });
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

function lahee_play_unlock_sound(gid, uad){
    var ach = null;
    for (var a of lahee_game.Achievements) {
        if (a.ID == uad.AchievementID) {
            ach = a;
            break;
        }
    }

    if (ach) {
        lahee_select_ach(gid, ach.ID);
        
        if (Notification.permission == "granted"){
            new Notification("Achievement Unlocked!", { body: ach.Title + " (" + ach.Points + ")", icon: ach.BadgeURL });
            lahee_audio_play("162482__kastenfrosch__achievement.mp3");
        }
    }
}

function lahee_audio_play(audio){
    if (Date.now() < lahee_last_audio + 1000){
        console.log("not playing audio, too close to previous audio");
        return;
    }
    try {
        var sound = new Audio("sounds/" + audio);
        sound.play();
    }catch (e){
        console.warn("Failed playing audio", e);
    }
    lahee_last_audio = Date.now();
}