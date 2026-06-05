# Build & package

How the deployable loader is assembled. CI runs this on release (see `.github/workflows/`).

## Prerequisites

- **.NET 8 SDK** (build floor — BepInEx master uses C# 12 collection expressions). Pinned via [`global.json`](global.json). The loader still **targets** net35/net46/net6 (output TFMs).
- NuGet feeds: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (declared in [`nuget.config`](nuget.config)).

## Sources

The BepInEx framework source is vendored **in this repo** under `BepInEx/` (on the `BepInEx` branch); the
interop and dumper are sibling branches of this same repo. The upstream `HetCreep/*` forks are for
give-and-take only — the loader builds from our own branches so an upstream PR can never move what we ship.

| Source | What it provides |
|---|---|
| `BepInEx/` (this repo, `BepInEx` branch) | core framework: preloader, chainloader, Unity IL2CPP runtime, doorstop glue (upstream BepInEx 6 master + our robustness patches) |
| `Il2CppInterop` branch (v1.5.2 + scan-safety fix) | the interop runtime trio (Runtime / Common / HarmonySupport) |
| `Il2CppDumper` branch (c01ns, metadata-v39) | metadata parser — **offline interop generation only**, not shipped to players |

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
from the game's own loaded type metadata (via Cpp2IL / Il2CppInterop + the c01ns v39 parser). They are
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
├── dxgi.dll                      # UnityDoorstop proxy
├── doorstop_config.ini
├── dotnet/                       # bundled CoreCLR host
└── BepInEx/
    ├── core/                     # the loader (BepInEx + Il2CppInterop + dobby)
    ├── interop/                  # pre-baked, offline-generated (106 DLLs)
    ├── config/BepInEx.cfg        # UpdateInteropAssemblies=false
    ├── plugins/                  # (translation plugins ship separately)
    └── patchers/
```

Output: `BepInEx.Priconne_<date>.zip` + its `.sha256`. Reproducibility: SDK pinned (`global.json`),
NuGet locked (`packages.lock.json` once the build is wired in CI), source commits recorded per release.
