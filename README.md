# Cpp2IL — IL2CPP metadata parser (component slot)

This branch marks the **Cpp2IL** component of BepInEx.Priconne — the dumper/parser rail, switched
here from `c01ns/Il2CppDumper` on 2026-06-06 ("สลับรางไป Cpp2IL").

Cpp2IL is the IL2CPP **metadata parser**: it reads the game's `global-metadata.dat` + `GameAssembly.dll`
and produces the dummy assemblies that Il2CppInterop converts into interop assemblies. In this loader it
runs **offline only** — during interop generation; the shipped player runs `UpdateInteropAssemblies=false`,
so Cpp2IL never executes on a player machine.

## How it is consumed — a fetched NuGet dependency (not vendored source)

Unlike the `BepInEx` and `Il2CppInterop` component branches, Cpp2IL is **not vendored as source** — it is
a fetched, BepInEx-endorsed NuGet package, pinned in
`BepInEx/Runtimes/Unity/BepInEx.Unity.IL2CPP/BepInEx.Unity.IL2CPP.csproj`:

```
Samboy063.Cpp2IL.Core   2022.1.0-pre-release.21      # LibCpp2IL parses metadata v23-106, incl. v39
```

- **Upstream:** https://github.com/SamboyCoding/Cpp2IL (maintainer: ds5678). MIT-licensed.
- **Metadata-version gate:** the LibCpp2IL version in that package. To support a new IL2CPP metadata
  version, **bump `Samboy063.Cpp2IL.Core`** to a release whose LibCpp2IL supports it — *not* a branch swap.
  (Pinned to an immutable released tag for reproducibility; the prior `-development.NNNN` was a moving CI build.)

## Give-and-take

Per the program rule, we do **not** fork until a real, reproduced bug. If a Cpp2IL / LibCpp2IL bug is
hit on our path (Unity 6 / metadata v39 / Windows-x64), fork `SamboyCoding/Cpp2IL` into `HetCreep`, land
the fix, and PR upstream citing the filed issue. This branch is the designated landing spot for that work.
The program's give-and-take watch list (open upstream issues by code path; e.g. BepInEx #1266 = the
metadata-v39 wall this rail-switch answers) lives in the governance hub at `docs/give-and-take-watch.md`.

## Lineage & credit

This slot was previously **c01ns/Il2CppDumper** (Perfare lineage, MIT) — the reference parser that first
established metadata-**v39** for this build (Gate-2). Cpp2IL (v39-capable since `pre-release.21`) is the
BepInEx-endorsed, actively-maintained parser, so the rail moved to it. The c01ns credit is retained in
the `NOTICE` file (on the `main` branch); the old vendored branch is archived at the git tag
`archive/il2cppdumper-c01ns` (recoverable).
