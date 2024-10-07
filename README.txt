L.A.H.E.E. - Local Achievements Home Edition Enhanced
RetroAchievements API Emulator
2024 Haruka
Licensed under the SSPL.

No support.

--------------------------------------------------

What???

This project is only usable for a very certain use case.
(which is, until RA implements a feature like that)

* Making a private copy of an achievement set, modify it, and play through it locally.

I needed this project because:

* The Final Fantasy Crystal Chronicles achievements are manually flagged to trigger only 
  in single player, so I was forced to remove the flag that checks for single player.
  Most of them worked absolutely fine in Multi with no additional changes, very few just
  checked P1 and only one of them did not work.
* You cannot upload achievements privately, neither have untracked forks of them.
 - Unofficial seems to be a global, shared category, but the docs don't say so idk.
* Local progress is not saved between sessions.
* You cannot play multiple subsets at once.
* I really can't be bothered to get developer status (and retain it).

THIS DOES NOT FORWARD ACHIVEMENTS TO THE REAL SITE. EVERYTHING IS LOCAL.

--------------------------------------------------

Usage:

This has been tested only with Dolphin 2409-108.

Unfortunately, Dolphin does not support overriding RetroAchievements.ini with the -C flag,
so you need to go to <your user data location>/Config/RetroAchievements.ini
and append a new entry named

HostUrl = http://localhost:8000/lahee