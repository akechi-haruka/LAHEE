# L.A.H.E.E. - Local Achievements Home Edition Enhanced

RetroAchievements Service Emulator

2024-2026 Haruka

Licensed under the SSPL.

No support.

---

## What???

This allows local/offline/modded progression of RetroAchievements.

### Screenshots

![https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot.jpg](https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot.jpg)
![https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot2.png](https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot2.png)
![https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot3.png](https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot3.png)
![https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot4.png](https://github.com/akechi-haruka/LAHEE/blob/master/Readme/screenshot4.png)

### Features

* Obtain achievements fully offline without an internet connection.
* Add or modify achievements (ex. remove single player checks from Final Fantasy Crystal Chronicles achievements), presence text (ex. Add a point counter display to FFCC)
* Merge sub-sets to one set (to play multiple challenges at once)
* Add custom ROM hashes (to use graphic patches, undubs, mods, etc.)
* Fetch achievement data and previous online progression from your real RetroAchievements account.
* Playtime tracking (approximate) and stats.
* Switch between multiple accounts and/or multiple progressions in the same game.
* Track progression for unofficial and local achievements.
* Automatic replays (requires OBS) or screenshots when achievements are obtained.
* View achievement code with explanations and code notes.
* Freely share your modified achievement data .json files to anyone.

### What LAHEE doesn't do

* LAHEE does NOT send achievements you've made on LAHEE to the real RetroAchievement site.
* LAHEE does NOT have rules you need to be aware of.
* LAHEE does NOT require you to talk to anyone to create or edit achievements.
* LAHEE does NOT broadcast your activity to the world.

## Usage

Latest stable release: https://github.com/akechi-haruka/LAHEE/releases

Latest unstable development build: https://nightly.link/akechi-haruka/LAHEE/workflows/dotnet/master

### Dolphin

1. Go to `<your user data location>\Config\RetroAchievements.ini`
and append a new entry named
`HostUrl = http://localhost:8000/`
2. Go to the directory where `Dolphin.exe` is located. If there are any `RA_Integration` files:
    1. If you want to be able to edit achievements, see "Patching RAIntegration".
    2. Otherwise delete those files.
3. Launch Dolphin, navigate to Tools > Achievements, and type in any desired username and any password.

### RetroArch

RetroArch has no ability to change the RA server name, so we need to patch that in the good old way.

1. Download https://github.com/akechi-haruka/hexedit2 and place the .exe next to `retroarch.exe`.
2. Create a backup copy of `retroarch.exe`
3. Open a command prompt in the folder where RetroArch is located and execute `hexedit2 multi -t StringASCII retroarch.exe retroarch.exe https://retroachievements.org http://localhost:8000`
4. Launch RetroArch, navigate to the achievements menu, and type in any desired username and any password.

## Web Viewer

Open http://localhost:8000/Web/ in your browser to see locked/unlocked achievements, records, status message and score.

To enable notifications and sound effects, allow them in your browser.

### Misc. features

If desired, an avatar can be placed in `<lahee root>\UserPic\<username>.png`.

## Adding achievements (from real site)

1. Open "appsettings.json".
2. Set "WebApiKey" to the Web API Key found on https://retroachievements.org/settings, aswell as "Username" and "Password" to your real RetroAchievements account.
3. After starting LAHEE, type in `fetch XXXX` where XXXX is the game ID you want to get achievements for. This ID can be found in the URL of the achievements page (ex. for FFCC: https://retroachievements.org/game/3885, this would be 3885).

Alternatively, you can also install http://localhost:8000/lahee.user.js, if you are running a userscript manager to add
a button to the real RetroAchievements website to import sets directly to LAHEE.

## Adding achievements (via RAIntegration)

From LAHEE 1.11 and above, you can use RAIntegration / "RetroAchievements Development" to add and modify achievements
directly in LAHEE via the Upload/Promote/Demote buttons.

### Patching RAIntegration

These steps are only required if your emulator loads RAIntegration from `RA_Integration[-x64].dll` or if you want to use
RAIntegration to edit achievements on LAHEE.

1. Download https://github.com/akechi-haruka/hexedit2 and place the .exe next to `Dolphin.exe`.
2. Run following:
   ```
   hexedit2 multi -t StringASCII RA_Integration.dll RA_Integration.dll https://retroachievements.org/dorequest.php http://localhost:8000/dorequest.php
   hexedit2 multi -t StringASCII RA_Integration.dll RA_Integration.dll https://retroachievements.org http://localhost:8000
   hexedit2 multi -t StringASCII RA_Integration-x64.dll RA_Integration-x64.dll https://retroachievements.org/dorequest.php http://localhost:8000/dorequest.php
   hexedit2 multi -t StringASCII RA_Integration-x64.dll RA_Integration-x64.dll https://retroachievements.org http://localhost:8000
   ```

## Adding achievements (manually / custom)

Achievement definitions must be placed in `<root>\<Data>\<gameid>-<optional_label>.<extension>`

- The optional label is simply for file organization convenience
  (ex. `3885-FFCCSubsetRareItems.json`)
- For core sets, simply copy the `####.json` file from `RAIntegration\Data`.
- For user sets, simply copy the `####-User.txt` file from `RAIntegration\Data`.
    - Latest supported RAIntegration version: 1.4.0.
- Achievement images must be placed in `<root>\lahee\Badge\<badgeid>.png`
- Game hash defitions must be placed in `<root>\<Data>\<gameid>-<optional_label>.hash.txt`
- Every line should depict one valid hash for this game+achievement set combo
- All `.zhash` files of the same ID are merged.

Note that the Game ID from the file name itself will override the Game ID that is stored inside the .json itself. This allows you to easily merge sets.

For example, to merge the core FFCC set (ID 3885) and the "Rare Drops" subset (ID 28855), name both files simply a variation of `3885-FFCC Core.json` and `3885-FFCC Rare Drops.json`, and both will be combined into one set and no longer requires patching your game hash.

## Capture Triggers

To define events (like screenshots or messages to OBS), define entries in appsettings.json under "Capture" as follow:

`{
"Trigger": string,
"Parameter": string,
"Delay": number
}`

Delay is the amount of milliseconds since the achievement trigger when this event should happen. Events can happen multiple times as well, i.e. you can make a screenshot at 0ms and another one at 2000ms.

### Trigger: `Screenshot`
Parameters: "Desktop" or "Window"

Take a screenshot from either the entire desktop or the currently active Window. Screenshots are saved in the Capture directory next to LAHEE.

### Trigger: `OBSWebsocket`
Parameters: The messageType to be sent to obs-websocket. See OBS' websocket's documentation for more information.

Sends a message to OBS, mostly used with parameter "SaveReplayBuffer".

## Config

To not have your changes overwritten on updates, you can create a copy of `appsettings.json` named
`appsetings.local.json` and make your changes there.

### LAHEE

* WebPort: the TCP port LAHEE runs under. If changed, emulator config or hexedits need to be adjusted.
* AutoSessionOnSingleUser: if only one user exists, LAHEE completely ignores authentication, and always logs the only
  existing user in, regardless of session tokens.
* LoadAsSingleSet: when true, all achievement sets will be merged to a single set.
* BadgeDirectory/DataDirectory/UserDirectory: Paths to folders LAHEE stores various files.
* PresenceHistoryLimit: The maximum number of presence (gameplay status) entries LAHEE should save. -1 for no limit.
  Might be cool to look back on the past, but set to a low number if you don't care or your save data becomes too big.
* RAFetch: Settings for downloading/fetching data from the official site, see "Adding achievements (from real site)"
* OBSWebsocketUrl: URL to your OBS instance for sending commands. Must not use authentication.

## Attribution

* `achievement.mp3` by Kastenfrosch -- https://freesound.org/s/162482/ -- License: Creative Commons 0
* `blank-sound.ogg` by JJ_OM -- https://freesound.org/s/540121/ -- License: Creative Commons 0
