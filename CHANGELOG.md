# Changelog

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
