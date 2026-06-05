# Build & package

How the deployable loader is assembled. CI runs this on release (see `.github/workflows/`).

## Prerequisites

- **.NET 6 SDK** (build floor; the repo also builds on 8/9/10). Pinned via [`global.json`](global.json).
- NuGet feeds: nuget.org + `nuget.bepinex.dev` + `nuget.samboy.dev` (declared in [`nuget.config`](nuget.config)).

## Sources (the loader spans two forks)

| Source | What it provides |
|---|---|
| [`HetCreep/BepInEx`](https://github.com/HetCreep/BepInEx) (`master`) | core framework: preloader, chainloader, Unity IL2CPP runtime, doorstop glue |
| [`HetCreep/Il2CppInterop`](https://github.com/HetCreep/Il2CppInterop) (v1.5.2 + scan-safety fix) | the interop runtime trio (Runtime / Common / HarmonySupport) |
| [`c01ns/Il2CppDumper`](https://github.com/c01ns/Il2CppDumper) | metadata-v39 parser — **offline interop generation only**, not shipped to players |

## Build

```bash
# 1. Il2CppInterop runtime (v1.5.2 + the Unity-6 FindSignatureInModule scan fix)
dotnet build Il2CppInterop/Il2CppInterop.Runtime/Il2CppInterop.Runtime.csproj -c Release

# 2. BepInEx IL2CPP core (NU5046 logo.png guard required)
dotnet build BepInEx/BepInEx.sln -c Release -p:GeneratePackageOnBuild=false
```

## Assemble `BepInEx/core/`

Copy the BepInEx IL2CPP build output (`BepInEx/bin/Unity.IL2CPP/*`) into `core/`, then overlay the
Il2CppInterop **v1.5.2+fix** trio (`Il2CppInterop.Runtime.dll` / `Il2CppInterop.Common.dll` /
`Il2CppInterop.HarmonySupport.dll`) and the native `dobby.dll`. The doorstop `dxgi.dll` +
`doorstop_config.ini` + the `dotnet/` CoreCLR host are added at the package root.

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
