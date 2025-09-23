// ==UserScript==
// @name        retroachievements.org LAHEE import shortcut
// @namespace   Violentmonkey Scripts
// @match       https://retroachievements.org/game/*
// @grant       none
// @version     1.0
// @author      Haruka Akechi
// @description Adds a shortcut to copy achievement data to a locally running LAHEE instance.
// ==/UserScript==

unsafeWindow.export_to_lahee = async function(){
    var id = window.location.href.split("/").at(-1);
    var target_id = prompt("Enter the game ID to save this set as:", id);
    if (!target_id){
        return;
    }
    var unofficial = confirm("Also include unofficial achievements?") ?? false;
    try {
        var resp = await fetch("http://localhost:8000/dorequest.php", {
            body: "r=laheetriggerfetch&gameid="+id+"&override="+target_id+"&unofficial="+unofficial,
            method: "POST"
        });

        if (!resp.ok) {
            throw new Error("Network request failed: " + resp.status);
        }

        alert("Request submitted.");
    }catch(e){
        alert("Communication with LAHEE failed: " + e);
    }
}

document.getElementsByClassName("flex  flex-col  gap-2")[1].innerHTML += `
<li>
    <a class="btn py-2 w-full px-3 inline-flex gap-x-2 transition-transform lg:active:scale-[97%]" onclick="export_to_lahee();">
        <span>Copy Set to LAHEE</span>
    </a>
</li>
`;