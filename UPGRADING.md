# Upgrading the BepInEx.Priconne loader

The loader is assembled on GitHub from upstream parts; almost nothing is carried by hand.
**BepInEx is pulled live from upstream master** on every CI/release run (nothing is vendored), the
interop is tracked automatically from the public loader (`auto-interop.yml`), and the only component
re-vendored by hand is our `Il2CppInterop` branch (upstream master plus two small deltas). Upgrading is
therefore: **read what moved → (rarely) re-vendor Il2CppInterop → boot-test → bump the baseline**.

## A. BepInEx moved (the `watch-upstream` issue fired)

BepInEx master is checked out fresh by `release.yml` and `ci.yml`, so the move is already in the next
release — there is nothing to re-vendor.

1. **See what moved.** The monthly `watch-upstream` workflow opens an issue when BepInEx master passes the
   recorded baseline (its cadence is ~1–2 builds/month — see `builds.bepinex.dev/projects/bepinex_be`).
2. **Read the new upstream commits** since `BEPINEX_BASE`. Look for anything in the preloader,
   chainloader, or IL2CPP runtime that could change boot behaviour.
3. **Boot-test the newest release** on the game install: deploy the release overlay and confirm it loads
   the plugins and translates. Releases cut by `auto-interop.yml` are prereleases precisely so they wait
   for this check.
4. **Bump `BEPINEX_BASE`** in `.github/workflows/watch-upstream.yml` as the review checkpoint. Interop does
   NOT need regenerating for a pure BepInEx bump (Unity unchanged).

**⛔ Never apply `patches/embedded-metadata-dumper.patch` to anything CI builds.** It is the OFFLINE
interop-generation tool (a `VirtualProtect` + `ReadProcessMemory` runtime memory dump = ban-risk). It is
forward-applied only in a separate offline interop-gen build, never in the player core (invariant 1,
ban-risk ≤ ImaterialC). The shipped core is stock upstream BepInEx, so the dumper cannot leak in unless
someone applies the patch on purpose.

## A2. Il2CppInterop moved — the one component re-vendored by hand

1. **Compare** upstream master with the component's base. For each delta we carry, check whether upstream
   already fixed the same thing — if so, drop our copy (do not re-apply what master already has).
2. **Re-vendor** into the `Il2CppInterop` branch: clone upstream master into a scratch directory, clear the
   branch's working tree (keep `.git`), copy the upstream source in (excluding `.git`, `bin`, `obj`,
   `.github`), then re-apply the deltas still missing upstream (the bounds-checked signature scan in the
   generator and the specific exception types in the runtime).
3. **Build-verify:**
   `dotnet build Il2CppInterop/Il2CppInterop.HarmonySupport/Il2CppInterop.HarmonySupport.csproj -c Release`
   — 0 errors, and the trio (Runtime / Common / HarmonySupport) is in the output.
4. **Check `git status` before committing.** A re-vendor overwrites `.gitignore` with upstream's and can
   sweep in stray local files (editor / agent state) — make sure `.claude/` is still ignored.
5. **Commit + push** the branch, dispatch `release` (or wait for the next auto release), boot-test, then
   **bump `INTEROP_BASE`** in `watch-upstream.yml`.

## B. Game updates its Unity (e.g. 6.0 → 6.x) — the heavier path

1. (If needed) check section A first.
2. **Regen interop** — via **Cpp2IL/LibCpp2IL** (the bundled parser) + Il2CppInterop, against the new game
   build's metadata (offline; never on a player). In practice the public loader's interop is tracked
   automatically; this step only matters if that source stops following the game.
3. **Re-derive the metadata signature** (Gate 1 — `MetadataSignatureToScan` / `MagicToFix` /
   `ObfuscatedMetadataHeaderOffset`). This is the maintainer's reverse-engineering step.
4. **Check the parser version if the IL2CPP metadata version bumped** (e.g. v39 → v40). The version gate is
   **Cpp2IL/LibCpp2IL**, *not* a branch swap — and its pin (`Samboy063.Cpp2IL.Core`) lives in upstream
   BepInEx's own `BepInEx.Unity.IL2CPP.csproj`, not ours. See whether upstream master already pins a
   release whose LibCpp2IL supports the new version; if not, that bump is an upstream change. (Upstream has
   reverted a newer pin once for regressions — verify before assuming newer is better.) Then boot-test.
5. **Build + boot-test** on the new game build.
6. **Cut a release.** Update `COMPATIBILITY.md` with the new verified game build.

## Why it stays simple
BepInEx and the interop are pulled from upstream as-is; the only thing we carry is two small Il2CppInterop
patches. The shipped `core` is stock BepInEx (ban-risk ≤ ImaterialC); the dumper is an offline tool applied
as a patch only when we generate interop — never compiled into the player's core.
