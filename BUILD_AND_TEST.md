# ChatChaos — Build & Test (step by step)

Goal: compile the mod into a `.dll`, make it multiplayer-compatible (Netcode Patcher),
install it in Lethal Company and test it.

> You do NOT need the game to compile (the game DLLs come from NuGet). You only need it
> to test. The easiest path is to let **GitHub** build it for you — see `GITHUB_BUILD.md`.

---

## 1. Prerequisites (install once)

1. **.NET SDK 8** (or 9) — https://dotnet.microsoft.com/download
   Then check in a terminal: `dotnet --version`
2. **Lethal Company** (Steam).
3. **r2modman** (mod manager) — https://thunderstore.io/c/lethal-company/p/ebkr/r2modman/
   - Create a dedicated **test profile** (e.g. `dev`).
   - In that profile, install **BepInExPack** (base dependency).
   - Launch the game once via r2modman so BepInEx generates its folders.

---

## 2. Configure the BepInEx NuGet source (once)

The project pulls BepInEx from a separate NuGet feed. The `.csproj` already declares it,
but if restore fails, add the source globally:

```bash
dotnet nuget add source https://nuget.bepinex.dev/v3/index.json --name BepInEx
```

---

## 3. Compile

In a terminal, go to the mod folder then:

```bash
cd "path/to/ChatChaos"
dotnet build -c Release
```

Expected result: `bin/Release/ChatChaos.dll`.

- If you get an error about `LethalCompany.GameLibs.Steam`: open `ChatChaos.csproj` and
  pin a precise package version matching your game build (see
  https://www.nuget.org/packages/LethalCompany.GameLibs.Steam).

---

## 4. Make the multiplayer RPCs work (Netcode Patcher)

The mod uses custom RPCs: they must be "patched". Two options.

### Option A — simplest for testing (runtime patch) ✅ recommended to start

1. In your r2modman profile, install the **Runtime Netcode Patcher** mod (by Ozone):
   https://thunderstore.io/c/lethal-company/p/Ozone/Runtime_Netcode_Patcher/
2. That's it: it patches the assemblies at launch. Compile normally (step 3), no extra
   handling of the `.dll`.
3. For publishing, add it to `manifest.json` dependencies.

### Option B — build-time patch (for a clean version to distribute)

1. Install the tool once:
   ```bash
   dotnet tool install -g Evaisa.NetcodePatcher.Cli
   ```
2. Build with the patch flag (the GitHub workflow does exactly this):
   ```bash
   dotnet build -c Release -p:NetcodePatch=true
   ```
3. The `.csproj` is already set to `DebugType=portable` (required, or the tool errors).

> For a first test, use **Option A**.

---

## 5. Install the mod

1. Find your test profile's plugins folder, like:
   `...\r2modman\LethalCompany\profiles\dev\BepInEx\plugins\`
2. Create a `ChatChaos` subfolder and copy `ChatChaos.dll` into it
   (and later `manifest.json` + `icon.png` + `README.md` for a full package).

---

## 6. Launch and check it loads

1. Launch the game **via r2modman** (test profile).
2. The BepInEx console should show:
   ```
   ChatChaos v0.1.0 by Remilulz_91 loaded. 6 event(s) registered. Language: ...
   ```
   If you don't see the console: enable the BepInEx log/console in r2modman, or check
   `BepInEx/LogOutput.log`.

---

## 7. Configure Twitch (host only)

1. After the first launch, open `BepInEx/config/Remilulz_91.ChatChaos.cfg`.
2. Set `Channel` (your Twitch login, lowercase) and `OAuthToken`
   (scopes `chat:read` + `chat:edit` — see the README).
3. Save. Restart the game (or it picks up the file on next launch).

On connect, the log shows:
```
Twitch: connected to #yourchannel as yourname (read + post).
```

---

## 8. Test in game (checklist)

Host a lobby, fly to a moon (not the Company building), and land:

- [ ] ~45 s after landing, the poll panel appears with 3 options and a 60 s countdown.
- [ ] Your chat receives the "new vote" message + the options line.
- [ ] Typing `1`, `2` or `3` in chat increments the matching bar (one vote per person).
- [ ] When the timer ends, the winner row turns green with a trophy, and chat gets the
      winner message.
- [ ] With nobody voting, a random option still wins.
- [ ] On the safe Company moon, no poll runs (with `SkipCompanyMoon = true`).

> The placeholder events only log for now (see `src/Events/EventLibrary.cs`). You'll see
> lines like `[Event] 'random_death' won the vote — placeholder effect` in the log.
> Replace each event body with the real effect as you build them.

---

## 9. Test in multiplayer

- Every player must have **the mod** (same version) and, if you chose Option A, the
  **Runtime Netcode Patcher**.
- Host + 1 client minimum. Check that the poll panel, the live counts, the countdown and
  the winner are **identical for everyone**.
- Only the host needs the Twitch token (the host is the streamer whose chat votes).

---

## 10. Common issues

| Symptom | Likely cause | Fix |
|---|---|---|
| Mod doesn't load | BepInEx missing / wrong folder | Check BepInEx install and the `plugins/` path. |
| Build error on GameLibs | Package version ≠ game version | Pin the version in the `.csproj`. |
| Twitch not connecting | Empty Channel/OAuthToken, or wrong token | Fill the config; regenerate the token with chat:read + chat:edit. |
| Panel/winner not synced in multiplayer | Netcode Patcher missing | Option A (Runtime Netcode Patcher) or Option B. |
| `netcode-patch` unreadable error | "full" debug symbols | Already set: `DebugType=portable`. |
| No poll on a moon | It's the Company moon, or no events registered | Expected on the safe moon; otherwise check the log. |
