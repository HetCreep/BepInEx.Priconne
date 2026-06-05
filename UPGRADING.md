# Upgrading the BepInEx.Priconne loader

The loader is a small Priconne-specific delta on top of upstream **BepInEx 6 (bleeding-edge)**.
Upgrading is not a rewrite — it is: **re-vendor the new upstream source → re-apply our patches → build →
boot-test**. Repeatable and scriptable.

## A. Bump BepInEx (the `watch-upstream` issue fired — upstream master moved)

1. **See what moved.** The monthly `watch-upstream` workflow opens an issue when BepInEx master passes the
   recorded baseline (its cadence is ~1–2 builds/month — see `builds.bepinex.dev/projects/bepinex_be`).
2. **Sync the upstream clone** (sibling `../BepInEx`, remotes `origin`=upstream, `fork`=HetCreep):
   ```
   git -C ../BepInEx fetch origin && git -C ../BepInEx checkout master && git -C ../BepInEx merge --ff-only origin/master
   ```
3. **Re-vendor into the `BepInEx` branch** — overwrite the framework source under `BepInEx/`:
   ```
   git -C ../BepInEx archive master | tar -x -C BepInEx
   ```
4. **Re-apply our patches** (`patches/*.patch`). Resolve any conflict at the patched site:
   ```
   git apply patches/embedded-metadata-dumper.patch   # offline-gen tool only; NOT in the shipped core
   ```
   (The robustness fixes #1331/#1335/#1336 are already committed into the source; re-vendoring overwrites
   them, so re-apply them too — keep them as `patches/` entries or cherry-pick from history.)
5. **Build-verify:** `dotnet build BepInEx/BepInEx.sln -c Release -p:GeneratePackageOnBuild=false` — green
   across all TFMs (net35/net46/net6). Needs the .NET 8+ SDK (BepInEx master uses C# 12); `global.json` pins it.
6. **Boot-test:** deploy `BepInEx/core` to the game install; confirm it loads the plugins + translates.
7. **Commit + push** the `BepInEx` branch, then **bump `BEPINEX_BASE`** in `.github/workflows/watch-upstream.yml`.
   Interop does NOT need regenerating for a pure BepInEx bump (Unity unchanged).

## B. Game updates its Unity (e.g. 6.0 → 6.x) — the heavier path

1. (If needed) bump BepInEx first (section A).
2. **Regen interop** — Cpp2IL / the dumper against the new game build's metadata (offline; never on a player).
3. **Re-derive the metadata signature** (Gate 1 — `MetadataSignatureToScan` / `MagicToFix` /
   `ObfuscatedMetadataHeaderOffset`). This is the maintainer's reverse-engineering step.
4. **Swap the dumper** if the IL2CPP metadata version bumped (e.g. v39 → v40) — use a parser that supports it.
5. **Build + boot-test** on the new game build.
6. **Commit + push + cut a release.** Update `COMPATIBILITY.md` with the new verified game build.

## Why it stays simple
Our delta is a few small patches on a large, battle-tested upstream. We re-vendor + re-apply rather than
fork-and-diverge. The shipped `core` builds from stock BepInEx (ban-risk ≤ ImaterialC); the dumper is an
offline tool applied as a patch only when we generate interop — never compiled into the player's core.
