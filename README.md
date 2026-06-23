# ChatChaos

Let your **Twitch chat decide what happens** during a Lethal Company run. Shortly
after you land on a moon, a poll opens: three random events appear on screen and in
your chat, and viewers vote by typing **1**, **2** or **3**. When the timer ends, the
winning event hits the whole crew.

Author: **Remilulz_91** — © 2026 Remilulz_91, all rights reserved.

> ⚠️ **Multiplayer: EVERY player must install this mod (same version).** It adds
> synced behaviour (the poll panel + the winning event), so it won't sync if only the
> host has it. Solo play is fine. Only the **host** needs to set up the Twitch token —
> the host is the streamer whose chat votes.

---

## What it does

- **Polls during the run.** On any moon (except the free Company building), a poll
  opens **45 s after landing** (configurable). It lasts **60 s**.
- **Three random events.** Each poll picks 3 events at random from the catalogue —
  some good for you, some not. Viewers vote **1 / 2 / 3**.
- **One vote per viewer.** Only a person's first valid message counts.
- **On screen, for everyone.** The orange poll panel shows the options, live vote
  counts and a countdown, then the winner (in green) when voting ends.
- **In chat too.** The mod posts the poll start, the options and the winner in your
  Twitch chat, under your own account.
- **In the game chat as well.** The same poll messages appear in the in-game text chat,
  visible to every player in the lobby (can be turned off in the config).
- **Nobody voted?** Fate decides — the mod picks a random option.
- **Multi-language.** The panel and chat messages are in **French** when the game is
  in French, **English** otherwise (override in the config).

> YouTube support is planned for a later version. For now the integration is Twitch.

---

## Quick setup (host / streamer)

1. Install the mod (see **Install** below) and launch the game once so it creates its
   config file: `BepInEx/config/Remilulz_91.ChatChaos.cfg`.
2. Generate a **Twitch OAuth token** for your account with the scopes
   **`chat:read`** and **`chat:edit`** (see **Getting a Twitch token** below).
3. Open the config file and fill in:
   - `Channel` = your Twitch login (lowercase), e.g. `remilulz_91`
   - `OAuthToken` = the token you generated
   - (`Username` can stay empty — it defaults to the channel)
4. Start a game and host a lobby. Land on a moon and watch the poll appear on screen
   and in your chat.

The token is a **secret**. Keep it in your local config only. It is git-ignored so it
can never end up in the repository, it only grants chat read/write, and you can revoke
it at any time from your Twitch account.

---

## Getting a Twitch token

You need a token tied to **your** account with the chat scopes `chat:read` +
`chat:edit`. Use a trusted token generator, for example:

- **https://twitchtokengenerator.com** — pick the scopes `chat:read` and `chat:edit`,
  authorize with your Twitch account, and copy the **Access Token**.

Paste it into `OAuthToken` in the config. The `oauth:` prefix is optional — the mod
adds it if missing. If you ever suspect your token leaked, revoke it from
**Twitch → Settings → Connections** and generate a new one.

---

## Configuration

All options live in `BepInEx/config/Remilulz_91.ChatChaos.cfg`:

| Section | Key | Default | Meaning |
|---|---|---|---|
| Twitch | `Enabled` | `true` | Turn the integration on/off. |
| Twitch | `OAuthToken` | _(empty)_ | **Secret.** Your chat token (`chat:read` + `chat:edit`). |
| Twitch | `Channel` | _(empty)_ | Your Twitch login, lowercase. |
| Twitch | `Username` | _(empty)_ | Account login; empty = same as Channel. |
| Twitch | `UseSSL` | `false` | Connect over TLS (port 6697) instead of 6667. |
| Poll | `DelayAfterLanding` | `45` | Seconds after landing before the first poll. |
| Poll | `Duration` | `60` | Voting time in seconds. |
| Poll | `RepeatInterval` | `0` | Seconds between polls on the same moon. `0` = one poll per landing. |
| Poll | `ResultDisplayDuration` | `6` | How long the winner panel stays up. |
| Poll | `SkipCompanyMoon` | `true` | No polls on the safe Company moon. |
| Poll | `RequireConnectedAccount` | `false` | If on, polls only run when a chat account is connected. |
| Chat | `AnnounceInChat` | `true` | Post poll messages in chat. |
| Chat | `Prefix` | `[ChatChaos]` | Prefix on every posted message. |
| Chat | `ShowInGameChat` | `true` | Also show poll messages in the in-game chat (all players). |
| Chat | `InGameChatColor` | `F0A91E` | Hex colour of the prefix in the in-game chat. |
| Display | `Language` | `Auto` | `Auto`, `English` or `French`. |
| Display | `PanelAnchorX/Y` | `0.30 / 0.22` | Panel position (0–1 across the screen). |
| Display | `PanelScale` | `1.0` | Panel size. |
| Display | `TimerPanelAnchorX/Y` | `0.985 / 0.5` | Position of the effect-countdown panel (right-centre). |
| Debug | `VerboseLogging` | `false` | Extra detailed logs (every vote, every broadcast) for debugging. |

---

## Install

Easiest with **r2modman** (or any mod manager): select Lethal Company, install
ChatChaos, and BepInEx comes along. Launch with **Start modded**.

Make sure **every player in the lobby** has the mod, same version.

---

## For developers

- Add or edit events in **`src/Events/EventLibrary.cs`** — one `EventRegistry.Add(...)`
  per event; the lambda is the in-game effect (runs on the host).
- Build, test and publish guides: `BUILD_AND_TEST.md`, `GITHUB_BUILD.md`,
  `PUBLISH_THUNDERSTORE.md`.

---

## Credits

Inspired by **Sehelitar**'s Twitch-integration mod (moderator for **MrTiboute**), which
gave me the idea and the design direction for this mod. Thanks!

---

## License & Copyright

© 2026 **Remilulz_91**. All rights reserved.

You may download, play, and contribute to this mod (issues, pull requests). You may
**not** claim authorship/ownership of it or its code, or redistribute it as your own
work, without the author's permission. The mod remains credited to and owned by
Remilulz_91.
