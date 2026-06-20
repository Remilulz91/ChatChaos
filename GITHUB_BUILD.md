# Build the mod automatically with GitHub (nothing to install)

The repo ships a GitHub Actions workflow (`.github/workflows/build.yml`) that, on every
push, **compiles the mod, netcode-patches it, and gives you the ready-to-use `.dll`**.
You don't need .NET on your machine.

---

## A. Put the code on GitHub (once)

1. Create a GitHub account if needed (https://github.com) and install **Git**:
   https://git-scm.com/downloads
2. On GitHub, click **New repository** → name it `ChatChaos` → **Create**
   (don't tick anything, no README).
3. Open a terminal **in the mod folder** (the one containing `ChatChaos.csproj`):

```bash
cd "path/to/ChatChaos"
git init
git add .
git commit -m "ChatChaos initial"
git branch -M main
git remote add origin https://github.com/Remilulz91/ChatChaos.git
git push -u origin main
```

(GitHub username: `Remilulz91`. The Thunderstore team stays `Remilulz_91`.)

---

## B. Get the compiled `.dll`

As soon as you push, GitHub runs the build automatically.

1. On your repo → **Actions** tab.
2. Click the latest **"Build ChatChaos"** run (green dot = success).
3. At the bottom, **Artifacts** → download **`ChatChaos`**.
4. Unzip: you get `ChatChaos.dll` (+ `manifest.json`, `icon.png`, `README.md`...).

The `.dll` is already **netcode-patched**: drop it into
`...\BepInEx\plugins\ChatChaos\` and play (see `BUILD_AND_TEST.md`).

---

## C. (Handy) Create a real downloadable Release

```bash
git tag v0.1.0
git push origin v0.1.0
```

GitHub then creates a **Release** with `ChatChaos.dll` and `ChatChaos.zip` attached
(the **Releases** tab). Ideal for sharing the mod or uploading to Thunderstore.

---

## D. Update the mod afterwards

On every change:

```bash
git add .
git commit -m "what you changed"
git push
```

→ a new build runs; go back to **Actions** to grab the `.dll`.

To release a new version, bump the version number in the **3** files
(`manifest.json`, `ChatChaos.csproj`, `src/Plugin.cs`), add a `CHANGELOG.md` line,
then push a new tag (`v0.2.0`, etc.).

---

## If the build fails (red dot)

Open the run in **Actions** and look at which step is red:

- **"Restore" red**: often a `LethalCompany.GameLibs.Steam` version that's unavailable.
  In `ChatChaos.csproj`, pin a precise version matching your game build
  (see https://www.nuget.org/packages/LethalCompany.GameLibs.Steam).

- **"Build … netcode-patched" red**: the Unity/Netcode versions don't match the current
  game. Two options:
  1. Adjust the values in `ChatChaos.csproj` (`NcpUnityVersion`, `NcpNetcodeVersion`,
     `NcpTransportVersion`).
  2. **Simple plan B**: remove `-p:NetcodePatch=true` from `.github/workflows/build.yml`
     (the `.dll` won't be patched), then in-game install the **Runtime Netcode Patcher**
     mod (by Ozone), which patches at launch.

- **A `UnityEngine.UI`-related error** (rare): the on-screen panel uses Unity's uGUI,
  provided by `LethalCompany.GameLibs.Steam`. If a stripped build ever omits it, pin a
  GameLibs version that includes it, or tell me and I'll switch the panel to IMGUI.

In all cases, copy me the error message from the red step and I'll tell you what to change.
