# Changelog

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
