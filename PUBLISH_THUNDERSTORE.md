# Publish ChatChaos on Thunderstore (automatic via GitHub)

The GitHub workflow publishes to Thunderstore automatically **on every version tag**,
in addition to creating the Release. You only configure it **once**.

---

## A. Prepare Thunderstore (once)

1. Go to https://thunderstore.io and **log in** (Discord, GitHub or Overwolf).
2. Create a **Team** whose name is **exactly** your namespace: **`Remilulz_91`**.
   - Top menu → **Teams** → **Create Team** → name `Remilulz_91`.
   - ⚠️ This must match `namespace: Remilulz_91` in the workflow. If you pick another
     team name, also change `namespace` in `.github/workflows/build.yml`.
3. Create a **Service Account** (a robot account used to publish):
   - Team `Remilulz_91` → **Settings** → **Service Accounts** → **Add Service Account**.
   - You get a **token** starting with `tss_...`.
   - ⚠️ **Copy it immediately, it's shown only once.** Never put it in the code.

---

## B. Give the token to GitHub (once)

1. On your repo → **Settings** → **Secrets and variables** → **Actions**.
2. **New repository secret**:
   - **Name**: `TS_TOKEN`
   - **Secret**: paste the `tss_...` token
3. **Add secret**.

GitHub will use this secret to publish, without ever exposing it.

---

## C. Publish a version

Each time you want to release on Thunderstore:

1. Bump the **version number** (must be **higher** than the previous one, and identical
   in the 3 files):
   - `manifest.json` → `"version_number"`
   - `ChatChaos.csproj` → `<Version>`
   - `src/Plugin.cs` → `Version`
   - (and add a line in `CHANGELOG.md`)
2. Commit + tag + push:
   ```bash
   git add .
   git commit -m "v0.2.0"
   git push
   git tag v0.2.0
   git push origin v0.2.0
   ```
3. GitHub: **Actions** tab → the build runs, **compiles**, creates the **Release**, AND
   **publishes to Thunderstore**. When done, your mod is online at
   `https://thunderstore.io/c/lethal-company/p/Remilulz_91/ChatChaos/`.

Your friends can then install it in **one click** from r2modman (BepInEx and
dependencies install automatically).

---

## D. Dependencies shown on Thunderstore

They come from `manifest.json` → `dependencies`. Currently: **BepInExPack**.

If you ever build **without** the build-time netcode patch (plan B in `GITHUB_BUILD.md`),
add the Runtime Netcode Patcher dependency here so players get it automatically.

---

## E. If publishing fails

Look at the red **"Publish to Thunderstore"** step in Actions:

- **"package already exists" / version**: you didn't bump the version number.
  Thunderstore refuses to overwrite an existing version.
- **Team / namespace mismatch**: the Thunderstore team name doesn't match the
  `namespace` in the workflow.
- **Invalid / missing token**: check the `TS_TOKEN` secret.
- **Action version**: if `GreenTF/upload-thunderstore-package@v4.3` no longer exists,
  replace it with the latest version listed at
  https://github.com/marketplace/actions/upload-thunderstore-package

---

## F. Alternative: manual upload (no GitHub)

You can also publish by hand: grab `ChatChaos.zip` (from a Release or the artifacts),
then on Thunderstore → your team → **Upload package** → drop the zip. Handy for a first
test without touching the token.
