# Changelog

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
