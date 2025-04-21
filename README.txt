L.A.H.E.E. - Local Achievements Home Edition Enhanced
RetroAchievements API Emulator
2024-2025 Haruka
Licensed under the SSPL.

No support.

--------------------------------------------------

What???

This allows local/offline/modded progression of RetroAchievements.

Screenshot:
https://github.com/akechi-haruka/LAHEE/blob/master/screenshot.jpg

Features:
* Obtain achievements without an internet connection.
* Add or Modify achievements (ex. remove single player checks from Final Fantasy Crystal Chronicles achievements)
* Edit presence text (ex. Add a point counter display to FFCC)
* Merge sub-sets to one set (to play multiple challenges at once)
* Add custom ROM hashes (to use graphic patches, undubs, etc.)
* Fetch achievement data and previous online progression from your real RetroAchievements account.
* Does not require reading dozens of pages of rules or talking to any person.

THIS DOES NOT FORWARD ACHIEVEMENTS TO THE REAL SITE. EVERYTHING IS LOCAL.

-------------------------------------------------
Usage
-------------------------------------------------

This has been tested only with Dolphin.

Unfortunately, Dolphin does not support overriding RetroAchievements.ini with the -C flag, so you need to go to <your user data location>\Config\RetroAchievements.ini
and append a new entry named

HostUrl = http://localhost:8000/

Afterwards, navigate to Tools > Achievements, and type in any desired username. (password is ignored).

An avatar can be placed in <root>\lahee\UserPic\<username>.png

-------------------------------------------------
Adding achievements (from real site)
-------------------------------------------------

Open "appsettings.json".
Set "WebApiKey" to the Web API Key found on https://retroachievements.org/settings, aswell as "Username" and "Password" to your real RetroAchievements account.
After starting LAHEE, type in "fetch XXXX" where XXXX is the game ID you want to get achievements for. This ID can be found in the URL of the achievements page (ex. for FFCC: https://retroachievements.org/game/3885, this would be 3885).

-------------------------------------------------
Adding achievements (manually / custom)
-------------------------------------------------

If you have created custom achievements with an emulator using RAIntegration, copy the following files:

Achievement definitions must be placed in <root>\<Data>\<gameid>-<optional_label>.<extension>
 - The optional label is simply for file organization convenience
   (ex. 3885-FFCCSubsetRareItems.json)
 - For core sets, simply copy the ####.json file from RAIntegration\Data.
 - For user sets, simply copy the ####-User.txt file from RAIntegration\Data.
  - Latest supported RAIntegration version: 1.3.1.
Achievement images must be placed in <root>\lahee\Badge\<badgeid>.png
Game hash defitions must be placed in <root>\<Data>\<gameid>-<optional_label>.zhash
 - Every line should depict one valid hash for this game+achievement set combo
 - All .zhash files of the same ID are merged.

Note that the Game ID from the file name itself will override the Game ID that is stored inside the .json itself. This allows you to easily merge sets.
For example, to merge the core FFCC set (ID 3885) and the "Rare Drops" subset (ID 28855), name both files simply a variation of "3885-FFCC Core.json" and "3885-FFCC Rare Drops.json", and both will be combined into one set and no longer requires patching your game hash.

-------------------------------------------------
Web Viewer
-------------------------------------------------

Open http://localhost:8000/Web/ in your browser to see locked/unlocked achievements, records, presence and score.

To enable notifications and sound effects, allow them in your browser.

-------------------------------------------------
Attribution
-------------------------------------------------
achievement.mp3 by Kastenfrosch -- https://freesound.org/s/162482/ -- License: Creative Commons 0
blank-sound.ogg by JJ_OM -- https://freesound.org/s/540121/ -- License: Creative Commons 0