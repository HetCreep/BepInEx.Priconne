# Build & package

How the deployable loader is assembled. CI runs this on release (see `.github/workflows/`).

## Prerequisites

- **.NET 8 SDK** (build floor — BepInEx master uses C# 12 collection expressions). Pinned via [`global.json`](global.json). The loader still **targets** net35/net46/net6 (output TFMs).
- NuGet feeds: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (declared in [`nuget.config`](nuget.config)).

## Sources

The BepInEx framework source is vendored **in this repo** under `BepInEx/` (on the `BepInEx` branch); the
interop is a sibling `Il2CppInterop` branch. The parser (Cpp2IL) is a fetched NuGet dep — the `Cpp2IL`
branch is a component marker, not built source; the offline extract tool is the `PrincessDumper` sibling
branch (run offline, never shipped). The upstream `HetCreep/*` forks are for give-and-take only — the
loader builds from our own branches so an upstream PR can never move what we ship.

| Source | What it provides |
|---|---|
| `BepInEx/` (this repo, `BepInEx` branch) | core framework: preloader, chainloader, Unity IL2CPP runtime, doorstop glue (upstream BepInEx 6 master + our robustness patches) |
| `Il2CppInterop` branch (v1.5.2 + scan-safety fix) | the interop runtime trio (Runtime / Common / HarmonySupport) |
| Cpp2IL (`Samboy063.Cpp2IL.Core 2022.1.0-pre-release.21`, NuGet — bundled in BepInEx) | the IL2CPP **metadata parser** (supports v23–106 incl **v39**) — the version gates which metadata versions parse; runs **offline** for interop generation only, never on the player |

> The interop-gen parser is **Cpp2IL/LibCpp2IL** (a NuGet dep bundled in BepInEx), not a separate
> branch. **c01ns/Il2CppDumper** (Perfare lineage) was the reference parser that first established
> metadata-**v39** for this build (Gate-2) — credited in [NOTICE](NOTICE), not a build source.

## Build

```bash
# Quick compile-check (CI's build-green gate):
dotnet build BepInEx/BepInEx.sln -c Release -p:GeneratePackageOnBuild=false   # NU5046 logo.png guard
dotnet build Il2CppInterop/Il2CppInterop.HarmonySupport/Il2CppInterop.HarmonySupport.csproj -c Release  # pulls the trio

# Full deployable core (what release.yml does) — Cake fetches the native deps incl dobby.dll:
cd BepInEx && build.cmd --target MakeDist   # -> BepInEx/dist-il2cpp/BepInEx/core  (WITH dobby.dll)
```

## Assemble `BepInEx/core/`

Copy the **Cake MakeDist output** (`BepInEx/dist-il2cpp/BepInEx/core/*` — already contains `dobby.dll` and
the native runtimes) into `core/`, then overlay the Il2CppInterop **v1.5.2+fix** trio
(`Il2CppInterop.Runtime.dll` / `Il2CppInterop.Common.dll` / `Il2CppInterop.HarmonySupport.dll`) from the
HarmonySupport build output, and trim `core/runtimes/` to `win-x64`. The doorstop `dxgi.dll` +
`doorstop_config.ini` + the `dotnet/` CoreCLR host are added at the package root.

> Do NOT assemble from `bin/Unity.IL2CPP/` — that output lacks `dobby.dll` (a plain `dotnet build` never
> downloads it). Always assemble from the Cake **MakeDist** output. (This was the broken-release bug.)

## Interop (offline, game-derived type metadata)

The 106 pre-baked interop assemblies are **generated offline** on a machine that has the target game,
from the game's own loaded type metadata (via **Cpp2IL/LibCpp2IL** — the bundled parser — + Il2CppInterop). They are
**type metadata, not Cygames content**, and shipping them prebuilt is *lower* ban-risk than a runtime
dump on the player (which this build never does — `UpdateInteropAssemblies=false`). CI cannot generate
interop (no game in CI).

The interop is stored **separately from the code** as a versioned release asset
(`interop-<gamebuild>` → `interop.zip`), so `main` stays loader-code only and the game-derived bytes
never enter the source tree. The release workflow (`workflow_dispatch` → `interop_tag`) downloads the
asset for the build named in [COMPATIBILITY.md](COMPATIBILITY.md) and assembles it into the zip.

## Package layout (the release zip)

```
<game folder>/
├── dxgi.dll                      # UnityDoorstop proxy — MUST be the dxgi host (see note below)
├── doorstop_config.ini
├── dotnet/                       # bundled CoreCLR host
└── BepInEx/
    ├── core/                     # the loader (BepInEx + Il2CppInterop + dobby)
    ├── interop/                  # pre-baked, offline-generated (106 DLLs)
    ├── config/BepInEx.cfg        # UpdateInteropAssemblies=false
    ├── plugins/                  # (translation plugins ship separately)
    └── patchers/
```

> **⚠️ Proxy host: Priconne requires the `dxgi` doorstop proxy — NOT `winhttp`.** Priconne uses winhttp
> very early (DMM/DRM), and the `winhttp.dll` doorstop proxy makes the game **silently fail to launch**
> (verified by isolation: winhttp proxy → no launch; dxgi proxy → launches + injects). The game imports
> both dxgi and winhttp; use the **dxgi** host and keep no local `winhttp.dll` (the game falls back to the
> system winhttp). This is normal doorstop usage (it supports multiple proxy hosts to avoid such conflicts),
> not a doorstop bug. **UnityDoorstop releases ship only a winhttp proxy** (v4.5.0 has no dxgi variant), so
> the dxgi proxy must be built from doorstop source (dxgi proxygen) or vendored — auto-shipping it in CI is
> a TODO. A loader-CORE overlay release ships no proxy (the user keeps their own); state the dxgi requirement
> in user docs.

Output: `BepInEx.Priconne_<date>.zip` + its `.sha256`. Reproducibility: SDK pinned (`global.json`),
NuGet locked (`packages.lock.json` once the build is wired in CI), source commits recorded per release.
