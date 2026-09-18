# BepInEx.Priconne

An independent **BepInEx 6 (IL2CPP) mod loader** for **Princess Connect! Re:Dive** (JP / DMM,
64-bit IL2CPP). Built from the open BepInEx lineage so it depends on no single author — a
maintained, v39-metadata-capable loader that the display/translation mods of the Priconne
modding suite run on.

> Not affiliated with, endorsed by, or supported by Cygames or DMM. This loads **alongside**
> your own game install; it never alters the shipped game binaries. Modding an online game
> carries the disclosed ToS / ban risk — this loader stays at the **low-risk** end (translation
> and display only, no automation, no memory writes, no telemetry). See [Legal & ban-risk](#legal--ban-risk).

## What it is

- A files-only [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) (`dxgi.dll`) →
  BepInEx 6 preloader → chainloader stack that lets published IL2CPP plugins load at your own launch.
- **Pre-baked interop** model (like the existing public loader): the IL2CPP interop assemblies are
  generated **offline** and shipped, so a normal launch performs **no** runtime memory dump
  (`[IL2CPP] UpdateInteropAssemblies = false`). This keeps ban-risk at or below the existing loader.
- The translation plugins (XUnity AutoTranslator, the TL fixups) live in their **own** repos — this
  repo is the **loader only**.

## Supported game build

See [COMPATIBILITY.md](COMPATIBILITY.md). Current: **Unity 6000.0.58f2 · IL2CPP metadata v39 ·
.NET 6.0.7**. Each release names the exact build it was verified against.

## Install

1. Close the game.
2. Download the latest `BepInEx.Priconne_<date>.zip` from [Releases](../../releases).
3. Extract its contents into your game folder (the directory that contains
   `PrincessConnectReDive.exe`), keeping the structure — `dxgi.dll` lands next to the exe.
4. Launch the game through DMM Game Player as usual. First launch writes `BepInEx/LogOutput.log`.

Verify it loaded: `BepInEx/LogOutput.log` shows `Chainloader initialized` and your plugins listed.

## Uninstall

Delete `dxgi.dll` and the `BepInEx/` folder from the game directory. The game runs vanilla again;
no game files were modified.

## Troubleshooting

- **Nothing happens / no `BepInEx/` logs** — confirm `dxgi.dll` is next to `PrincessConnectReDive.exe`.
- **Crash on launch** — read `BepInEx/ErrorLog.log` (a fatal preloader/chainloader error is recorded
  there with an actionable message) and `BepInEx/LogOutput.log`.
- **`global-metadata.dat is not embedded` / interop out of date** — your game build moved past the
  shipped interop; you need a release verified against your current build (see COMPATIBILITY.md).

## Build from source

See [BUILD.md](BUILD.md). In short: build the [BepInEx](https://github.com/HetCreep/BepInEx) and
[Il2CppInterop](https://github.com/HetCreep/Il2CppInterop) forks, assemble `BepInEx/core/`, add the
offline-generated `interop/`, and package with the doorstop + `.NET` host. CI does this on release.

## Lineage & License

Fork of [BepInEx/BepInEx](https://github.com/BepInEx/BepInEx) — **LGPL-2.1** (see [LICENSE](LICENSE)).
Component lineage and attribution in [NOTICE](NOTICE): krulci (loader/dumper lineage),
[c01ns/Il2CppDumper](https://github.com/c01ns/Il2CppDumper) (the reference parser that established
metadata-**v39** for this build, Gate-2), [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) (the bundled
interop-gen parser) and [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop). Any derivative stays LGPL-compatible;
keep the LGPL notice and upstream attribution intact.

## Legal & ban-risk

- **Files-only.** The game's own on-disk binaries are never patched or modified; the loader runs alongside.
- **Type metadata, not content.** The interop assemblies are program-interoperability *type metadata*
  derived from the game's own type system — not Cygames content (art, audio, story). No game content
  is decrypted or redistributed.
- **No automation.** This loader ships and curates display / translation / convenience mods only —
  never auto-battle, farming, clickers, or any gameplay automation.
- **Telemetry-free.** The only outbound fetch is the Unity base libraries from the BepInEx CDN (build
  time); no analytics, no heartbeat, no new outbound host.
- "Princess Connect! Re:Dive" / "プリコネ" are trademarks of **Cygames, Inc.**; "DMM" of **DMM.com LLC** —
  nominative use only, no branding or logos.

Modding any online game can violate its Terms of Service and risks account action. Use at your own risk.
