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
  Twitch chat, under the account you configure (a dedicated bot account or your own).
- **In the game chat as well.** The same poll messages appear in the in-game text chat,
  visible to every player in the lobby (can be turned off in the config).
- **Nobody voted?** Fate decides — the mod picks a random option.
- **Multi-language.** The panel and chat messages are in **French** when the game is
  in French, **English** otherwise (override in the config).

---

## Quick setup (host / streamer)

Only the **host** (the streamer whose chat votes) needs to do this.

1. Install the mod (see **Install** below) and launch the game once so it creates its
   config file: `BepInEx/config/Remilulz_91.ChatChaos.cfg`.
2. Choose which account will **post** the announcements in your Twitch chat:
   - **Recommended — a separate bot account.** Create a free second Twitch account
     (e.g. `ChatChaosBot`). The messages appear under the bot's name, and your main
     account's token never goes in the config.
   - Or your own account, if you don't mind the announcements appearing under your name.
3. Generate a **chat token** for that account (see **Getting a Twitch token** below).
4. Open the config file, section `[Twitch]`, and fill in:
   - `Channel` = your **stream** login, lowercase — the channel where votes happen
     (e.g. `remilulz_91`)
   - `OAuthToken` = the token of the posting account
   - `Username` = the **login the token belongs to** (your bot's login). Leave empty
     only if it's the same account as `Channel`.
   - `UseSSL` = `true` (encrypts the token while connecting)
   - `AnnounceInChat` = `true` (posts the announcements in Twitch chat)
5. Host a lobby, land on a moon, and watch the poll appear on screen **and** in your chat.
   The logs will show `connected to #yourchannel as ... (read + post)` when it worked.

> **Read-only also works.** If you leave `OAuthToken` empty, the mod connects
> anonymously: it still **reads and counts votes**, it just can't **post** in your
> Twitch chat (the on-screen and in-game announcements still show). A token is only
> needed for the Twitch chat messages.

---

## Getting a Twitch token

> ⚠️ This is a **chat OAuth token**, *not* your **stream key**. Never put your stream
> key here — that key is only for broadcasting video and would let others stream on
> your channel.

The mod only needs one value: a chat token (`oauth:...`) with the scopes `chat:read` +
`chat:edit`. You do **not** need to create an app or extension in the Twitch Developer
Console.

**Simple way (recommended):**

1. Log in to Twitch with the **account that will post** (your bot account) — e.g. in a
   private/incognito window so it's not your main account.
2. Go to **https://twitchtokengenerator.com**.
3. Choose **"Bot Chat Token"** (it pre-selects `chat:read` + `chat:edit`).
4. Click **Generate** and authorize with that account.
5. Copy the **Access Token** and paste it into `OAuthToken`. The `oauth:` prefix is
   optional — the mod adds it if missing.

**Two tips for the posting account:**

- **Verify its email** (and phone if prompted). Twitch blocks chat messages from
  unverified accounts, so a brand-new bot can't post until it's verified.
- **Make it a moderator of your channel** (`/mod yourbot`). This avoids slow-mode,
  followers-only and rate limits that would otherwise stop it from posting quickly.

**Official alternative (no third-party site):** register an *Application* in the Twitch
Developer Console (left sidebar **Applications**, *not* Extensions) to get a Client ID,
then run the OAuth authorization flow yourself via `https://id.twitch.tv/oauth2/authorize`.
More technical, same result.

The token is a **secret**: keep it in your local config only (it's git-ignored so it
can never end up in the repository), it grants chat read/write only, and you can revoke
it any time from **Twitch → Settings → Connections**.

---

## Configuration

All options live in `BepInEx/config/Remilulz_91.ChatChaos.cfg`:

| Section | Key | Default | Meaning |
|---|---|---|---|
| Twitch | `Enabled` | `true` | Turn the integration on/off. |
| Twitch | `OAuthToken` | _(empty)_ | **Secret.** Your chat token (`chat:read` + `chat:edit`). |
| Twitch | `Channel` | _(empty)_ | Your **stream** login, lowercase (where votes happen). |
| Twitch | `Username` | _(empty)_ | Login the token belongs to (your bot). Empty = same as Channel. |
| Twitch | `UseSSL` | `false` | Connect over TLS (port 6697) instead of 6667. Recommended `true` when using a token. |
| Poll | `DelayAfterLanding` | `45` | Seconds after landing before the first poll. |
| Poll | `Duration` | `60` | Voting time in seconds. |
| Poll | `PollsPerMoon` | `2` | Polls per moon (morning + afternoon). |
| Poll | `AfternoonPollTime` | `0.45` | In-game time (0–1) the afternoon poll opens. |
| Poll | `ResultDisplayDuration` | `6` | How long the winner panel stays up. |
| Poll | `SkipCompanyMoon` | `true` | No polls on the safe Company moon. |
| Poll | `RequireConnectedAccount` | `false` | If on, polls only run when a chat account is connected. |
| Chat | `AnnounceInChat` | `true` | Post poll messages in chat. |
| Chat | `Prefix` | `[ChatChaos]` | Prefix on every posted message. |
| Chat | `ShowInGameChat` | `true` | Also show poll messages in the in-game chat (all players). |
| Chat | `InGameChatColor` | `F0A91E` | Hex colour of the prefix in the in-game chat. |
| Display | `Language` | `Auto` | `Auto`, `English` or `French`. |
| Display | `PanelAnchorX/Y` | `0.97 / 0.45` | Poll panel position (right side by default). |
| Display | `PanelScale` | `1.0` | Panel size. |
| Display | `TimerPanelAnchorX/Y` | `0.015 / 0.55` | Position of the effect-countdown panel (left side by default). |
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
