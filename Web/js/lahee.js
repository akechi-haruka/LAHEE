var LAHEE_URL;
var lahee_data;
var lahee_user;
var lahee_game;

function lahee_init() {
    LAHEE_URL = "http://localhost:8000/dorequest.php";
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
    document.getElementById(page).style.display = "block";
}

function lahee_postinit(res) {

    lahee_data = res;

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
    lahee_change_game();
    lahee_connect_liveticker();
}

function lahee_update_game_status() {
    lahee_request("r=laheeuserinfo&user=" + lahee_user.UserName + "&gameid=" + lahee_game.ID, function (res) {
        var msg;
        if (res.currentgameid == lahee_game.ID) {
            var lastping = new Date() - new Date(res.lastping);
            if (lastping < 600_000) {
                msg = res.gamestatus + "\nPlaytime: " + res.playtime;
            } else {
                msg = "Game last played: " + TimeSpan.fromMilliseconds(lastping).toShortString() + " ago";
            }
        } else {
            msg = "Not currently playing " + lahee_game.Title + ".";
        }
        document.getElementById("ingame").innerText = msg;

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

    lahee_user = user;
    lahee_game = game;

    document.getElementById("useravatar").src = "../UserPic/" + user.ID + ".png";
    document.getElementById("gameavatar").src = game.ImageIconURL;

    lahee_build_achievements(user, game);
}

function lahee_build_achievements(user, game) {
    var content = "<div class='ach_grid'>";

    var sort = document.getElementById("sort_select").value;
    var arr = game.Achievements.slice();

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

        content += `<img src="${status != 0 ? a.BadgeURL : a.BadgeLockedURL}" class="ach_type_${a.Type} ach_status_${status}" onclick="lahee_select_ach(${game.ID}, ${a.ID});" />`;
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
        unlockTime = ua.AchievePlaytime;
    } else if (ua.Status == 1) {
        status = "Unlocked";
        unlockDate = Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' }).format(new Date(ua.AchieveDateSoftcore * 1000));
        unlockTime = ua.AchievePlaytimeSoftcore;
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
        lahee_update_game_status();
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