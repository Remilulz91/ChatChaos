# Changelog

## 0.31.0
- New event: "Berserk (45s)" — part 1 (core). When it wins, a "RECEIVING SIGNAL -> GO
  BERSERK" overlay types out on screen; once it finishes, a random living player becomes
  invincible to everything (mobs, turrets, mines, fall...) for 45s, then back to normal.
  No music (kept out on purpose to avoid copyright/ban issues). Shotgun + unlimited ammo
  come in the next version.

## 0.30.0
- New event: "Random weather" / "Météo aléatoire" — picks a random weather from the
  current moon's possible weathers (prefers a change) and applies it. Visual effects
  (fog/rain/eclipse) are toggled best-effort; flooding/eclipse spawns are load-time and
  won't fully change mid-round. Synced to all.

## 0.29.0
- Logging system: centralised, tagged logs ([ChatChaos][Poll], [ChatChaos][Event], ...).
  Poll open/close now logs the moon and the 3 options; the winning event logs Applying ->
  OK/FAILED with the exact event id, so a failing event is easy to spot. Startup logs the
  full list of loaded events. New config Debug/VerboseLogging for per-vote / per-broadcast
  detail when hunting a bug.

## 0.28.0
- New events: "Turn power on" / "Allumer courant" and "Turn power off" / "Eteindre
  courant" — control the facility (dungeon) power. The breaker method is called by
  reflection (several candidate names) so the build can't break on a version difference;
  the log shows which one applied. Synced to all.

## 0.27.0
- New event: "Team revive" / "Résurrection équipe" — revives all dead players and
  teleports them back to the ship (via the game's ReviveDeadPlayers). Living players are
  left where they are. Synced to all.

## 0.26.0
- New event: "Ship locked (30s)" / "Vaisseau bloqué (30s)" — closes the hangar door and
  blocks the lever (no takeoff) for 30 seconds, then reopens and unblocks. Synced to all;
  the host owns the timer.

## 0.25.0
- "Stamina boost (1m)" is now BOOSTED (not unlimited): a flat per-second top-up makes the
  sprint meter drain slower and regenerate faster for 60s, while staying limited.

## 0.24.0
- New event: "Stamina boost (1m)" / "Stamina boostée (1m)" — unlimited sprint stamina for
  every player for 60 seconds (the sprint meter is kept full). Per-player, synced to all.

## 0.23.0
- New event: "Teleport to ship" / "Téléporter au vaisseau" — teleports every living
  player back to the ship, whether they are inside the dungeon or outside. Dead players
  are unaffected. Synced to every player.

## 0.22.0
- New event: "Time frozen (1m)" / "Temps figé (1m)" — stops the in-game day clock for 60
  seconds, then resumes it at the moon's normal speed. Synced to every player; the host
  owns the timer.

## 0.21.0
- "Lock doors": classic doors are now locked with the game's LockDoor() method (per
  machine), so the padlock is shown to every player (instead of only setting the locked
  state).

## 0.20.0
- New events: "Unlock doors" / "Déverrouiller portes" and "Lock doors" / "Verrouiller
  portes". Unlock removes the lock from locked classic doors (no key needed; players open
  them by hand) and opens the big metal terminal doors. Lock locks classic doors that
  aren't open and closes the big metal doors (reopenable from the terminal). Adapts to the
  dungeon (e.g. the manor has only classic doors). Synced to every player.

## 0.19.0
- New events: "Recharge equipment" / "Recharge équipements" (sets all battery items to
  100%) and "Discharge equipment" / "Décharge équipements" (sets them to 0%). Affects
  flashlights, walkie-talkies, etc. Synced to every player.

## 0.18.0
- Event system now supports DYNAMIC events whose label and effect are rolled when the
  option is drawn (so the chat sees the exact value that will apply).
- New events: "Scrap value -X%" / "Valeur scrap -X%" and "Scrap value +X%" /
  "Valeur scrap +X%", where X is rolled 5-50 each time. Changes the value of all scrap
  by that percentage, synced to every player.

## 0.17.1
- Fix build error: 'Object' was ambiguous (System.Object vs UnityEngine.Object) in the
  credit-sync code; fully qualified UnityEngine.Object.

## 0.17.0
- New poll option: "Double or nothing" / "Quitte ou double". When it wins, it ARMS a
  gamble that stays pending until the ship next visits the Company:
  - arriving at the Company shows a warning message,
  - leaving the Company (the lever) doubles or halves (50/50) ALL the terminal's group
    credits, with a green WON / red LOST message, then disarms.
  Credits are synced to every player; messages use the game's native note (no overlap
  with the poll panel, since polls never run at the Company).

## 0.16.0
- New event: "Max health" / "Santé max" — heals every living player back to full health
  (100 HP in vanilla), clearing the injured/bleeding state. Dead players are unaffected.
  Health is set on every machine so all copies stay in sync.

## 0.15.0
- New event: "1 HP" / "1 PV" — sets every living player to 1 HP (dead players are
  unaffected). Networked via the game's damage path so the health bar syncs to everyone.

## 0.14.0
- New event: "Items dropped" / "Objets lâchés" — every living player drops all the items
  they hold (dead players are unaffected). Networked: each machine drops its own player's
  items so the drops sync correctly.

## 0.13.0
- First real event implemented: "Random death" / "Mort aléatoire" now kills one random
  LIVING player (dead players and empty slots are excluded). Networked so the death is
  synced to everyone; each machine kills only its own player (the owner) so the game
  propagates the death correctly.

## 0.12.0
- Hardened the poll HUD against takeoff edge cases (voting and winner views):
  - More reliable takeoff detection (shipHasLanded AND not leaving AND not in orbit),
    so the poll pauses the instant the lever is pulled and never reacts to unrelated
    state (e.g. doors opening/closing).
  - Landing now clears any leftover panel (a frozen/cancelled or fading winner panel
    from a previous moon) before scheduling the next poll.
  - Safety net: a voting panel can no longer get stuck on screen — it self-clears if no
    result/hide ever arrives past the vote end.

## 0.11.0
- When a poll is frozen by takeoff during the last 10 seconds, the countdown number now
  keeps pulsing (black<->red + grow/shrink) on a loop while staying frozen, until the
  panel disappears after 10s. Counts stay frozen and no event is applied.

## 0.10.0
- Fixed the build error (duplicate local variable name in the HUD).
- Non-host players who configured an account now get a one-time on-screen notice (and a
  log line) telling them their account is ignored — only the host's chat drives the votes.
- New config option Poll/RequireConnectedAccount (default false): when on, polls only run
  if a chat account is connected; otherwise no poll is started (instead of running a poll
  with a random outcome).

## 0.9.0
- Voting bars now all use the same orange (the leading option is no longer highlighted
  with a brighter shade), matching the reference look.

## 0.8.0
- Countdown number is black normally. In the last 10 seconds it pulses once per second:
  it grows and turns red at each beat, then shrinks back to normal size and black, until
  zero (then it switches to the winner).
- Added a drawn clock/stopwatch icon next to the number; it pulses (scale + colour) in
  sync with the number.

## 0.7.0
- If the ship leaves the moon during a poll, the poll is now CANCELLED: the panel
  freezes (countdown and vote counts stop), stays on screen for the result duration
  (10s), then disappears completely. No winner is chosen and no event is applied.
- Polls that were only scheduled (still in the 45s delay) are dropped silently on
  takeoff. A poll already showing its winner keeps its normal 10s display.

## 0.6.0
- Fixed the poll panel sometimes staying stuck on screen after the result: the panel
  now hides itself with an internal timer (independent of the network), so it always
  disappears completely once the result has been shown.
- The result panel now stays for 10 seconds by default (was 6). Configurable via
  Poll/ResultDisplayDuration.
- Confirmed lifecycle: panel hidden until a poll starts, appears with live-updating
  vote counts during voting, shows the winner, then clears on its own.

## 0.5.0
- New on-screen tip when the Twitch connection is established (shown to all players):
  "ChatChaos - Twitch / Connected as {user}." (read-only variant when no token is set).
- Reworded the landing tip to mix the previous wording with the moon + countdown:
  "ChatChaos - Twitch active / Automatic polls are now active. Landed on {moon}:
  first vote in {delay}s."

## 0.4.0
- Reworded the winner announcement (Twitch + in-game chat) to:
  "Voting closed! Winner: {event} with {n} votes." / FR: "Vote terminé ! Gagnant :
  {event} avec {n} votes."

## 0.3.0
- Poll messages now also appear in the in-game text chat (landing, poll start +
  options, winner, takeoff), broadcast to every player in the lobby via the game's
  own chat. The mod prefix is shown in colour (configurable).
- New config options: `Chat/ShowInGameChat` (on by default) and
  `Chat/InGameChatColor`. The "last call" reminder stays Twitch-only to avoid spam.

## 0.2.0
- Chat announcement when the ship lands on a moon, with the moon's name and the
  countdown: "The ship has landed on {moon}. Voting starts in {delay} seconds."
- Chat announcement when the ship takes off: "The ship has taken off. Polls are
  interrupted." (only after a moon where polls were active).
- On-screen game tip on landing (the native bottom-left note), shown to all players,
  confirming the integration is live and synced.
- Credit added for Sehelitar's Twitch-integration mod (moderator for MrTiboute), the
  inspiration for this mod.

## 0.1.0
- Initial version: foundations of the Twitch-driven poll system.
- A poll opens 45 s after landing on any moon except the safe Company building;
  voting lasts 60 s (both configurable).
- Each poll picks 3 random events from the catalogue; viewers vote by typing
  1, 2 or 3 in chat. One vote per person; no votes = a random option wins.
- On-screen poll panel (the orange ">SONDAGE / >POLL" box): options, live vote
  counts, countdown, and a green winner row with a trophy when voting ends.
- Chat announcements (start, options, winner) posted under the host's account,
  prefixed with [ChatChaos].
- Multiplayer-synced (host drives the poll; the panel and the winning event are
  mirrored to all players).
- Multi-language: French when the game/system language is French, English otherwise
  (config override available).
- Extensible event system: add events in src/Events/EventLibrary.cs. Ships with
  placeholder events (random death, drop items, 1 HP, recharge equipment, power
  outage, random teleport) that only log for now — replace their bodies with real
  effects.
- GitHub Actions build: compiles, netcode-patches the DLL, creates a Release and can
  auto-publish to Thunderstore on a version tag.
