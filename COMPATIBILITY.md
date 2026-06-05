# Compatibility

This loader targets one game build at a time. IL2CPP interop is **build-specific** — a release
verified against one game version may not load on a newer one (the metadata/interop drift). Always
use a release whose verified build matches your install.

## Verified build

| Component | Version |
|---|---|
| Game | Princess Connect! Re:Dive (JP / DMM, 64-bit IL2CPP) |
| Unity | **6000.0.58f2** |
| IL2CPP metadata | **v39** |
| .NET runtime (game) | 6.0.7 |

## Loader components in this build

| Piece | Version / source |
|---|---|
| BepInEx core | **6.0.0** (upstream dev build `be.755`, commit `3fab71a`) + our reliability fixes — **Priconne improved edition** |
| Il2CppInterop | **v1.5.2** + the Unity-6 `FindSignatureInModule` scan-safety fix |
| Metadata parser | c01ns/Il2CppDumper (metadata v39) — **offline interop generation only** |
| Interop | pre-baked, offline-generated (106 assemblies), `UpdateInteropAssemblies=false` |

## When the game updates

A new game build (Unity patch / metadata bump) can invalidate the pre-baked interop. The symptom is
`global-metadata.dat is not embedded` or an "interop out of date" warning at launch. Resolution:
interop is regenerated **offline** against the new build and a new release is cut, naming the new
verified Unity / metadata version here. Each release states the build it was verified against.

> The loader and Il2CppInterop versions are tracked against upstream BepInEx; only the game-specific
> metadata signature + the regenerated interop change per game build.
