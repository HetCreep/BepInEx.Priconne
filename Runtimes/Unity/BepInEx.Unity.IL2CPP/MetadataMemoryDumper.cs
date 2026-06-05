using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BepInEx.Unity.IL2CPP;

/// <summary>
///     Locates and extracts IL2CPP metadata that has been obfuscated and embedded inside
///     <c>GameAssembly.dll</c> (e.g. games that hide the <c>global-metadata.dat</c> magic so the
///     standard on-disk file is absent). Instead of reading the on-disk metadata, it dumps the live
///     loaded module from process memory, restores a file-like PE layout, scans for a configured byte
///     signature to find the embedded metadata, and repairs its magic so a dumper can parse it.
/// </summary>
/// <remarks>
///     Ported from the krulci/c01ns Priconne lineage (<c>Il2CppInterop.Runtime.MemoryUtils</c>).
///     The signature/offset/magic are game-build-specific and supplied via the <c>[IL2CPP]</c> config
///     (<c>MetadataSignatureToScan</c> / <c>MagicToFix</c> / <c>ObfuscatedMetadataHeaderOffset</c>);
///     deriving them for a given build is reverse-engineering work and out of scope here — this only
///     provides the mechanism that uses them.
/// </remarks>
internal static class MetadataMemoryDumper
{
    private const uint PAGE_EXECUTE_READWRITE = 0x40;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
                                                 int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    private static void GetModuleRegions(System.Diagnostics.ProcessModule module,
                                         out List<MEMORY_BASIC_INFORMATION> protectedRegions)
    {
        protectedRegions = new List<MEMORY_BASIC_INFORMATION>();
        var moduleEndAddress = (IntPtr) ((long) module.BaseAddress + module.ModuleMemorySize);
        var currentAddress = module.BaseAddress;
        while (currentAddress.ToInt64() < moduleEndAddress.ToInt64())
        {
            var result = VirtualQuery(currentAddress, out var memoryInfo,
                                      (uint) Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));
            if (result == 0)
                break; // error or reached the end of the module's memory space
            protectedRegions.Add(memoryInfo);
            currentAddress = (IntPtr) ((long) memoryInfo.BaseAddress + (long) memoryInfo.RegionSize);
        }
    }

    private static void SetModuleRegions(ILogger logger, List<MEMORY_BASIC_INFORMATION> protectedRegions,
                                         uint? newProtection = null)
    {
        foreach (var region in protectedRegions)
        {
            var result = VirtualProtect(region.BaseAddress, (uint) region.RegionSize,
                                        newProtection ?? region.Protect, out _);
            if (!result)
                logger.LogError("VirtualProtect failed with error code {Error}", Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    ///     Dumps <c>GameAssembly.dll</c> from the current process's memory and extracts the embedded,
    ///     obfuscated IL2CPP metadata located via <paramref name="metadataSignatureToScan" />.
    /// </summary>
    public static void RuntimeModuleDump(ILogger logger, out byte[] il2cppBytes, out byte[] metadataBytes,
                                         byte[] metadataSignatureToScan, byte[] magicToFix,
                                         int metadataSignatureOffset = 252)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var module = process.Modules.OfType<System.Diagnostics.ProcessModule>()
                            .FirstOrDefault(x =>
                                string.Equals(x.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(x.ModuleName, "GameAssembly.so", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(x.ModuleName, "GameAssembly.dylib", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(x.ModuleName, "UserAssembly.dll", StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidOperationException(
                                "Could not locate the IL2CPP game assembly module (GameAssembly.dll/.so/.dylib or UserAssembly.dll) in the current process.");

        var moduleBytes = new byte[module.ModuleMemorySize];
        GetModuleRegions(module, out var protectedRegions);
        SetModuleRegions(logger, protectedRegions, PAGE_EXECUTE_READWRITE);
        bool read;
        try
        {
            read = ReadProcessMemory(process.Handle, module.BaseAddress, moduleBytes, module.ModuleMemorySize, out _);
        }
        finally
        {
            // Always restore the original page protections — even if the read throws/fails — so the
            // module is never left PAGE_EXECUTE_READWRITE.
            SetModuleRegions(logger, protectedRegions);
        }

        if (!read)
        {
            logger.LogError("Failed to read process memory of {Module}", module.ModuleName);
            il2cppBytes = Array.Empty<byte>();
            metadataBytes = Array.Empty<byte>();
            return;
        }

        // Restore a file-like PE layout: in a loaded module the sections are mapped by VirtualAddress,
        // so rewrite each section's PointerToRawData/SizeOfRawData to its VirtualAddress/VirtualSize.
        using (var stream = new MemoryStream(moduleBytes))
        using (var reader = new BinaryReader(stream))
        using (var writer = new BinaryWriter(stream))
        {
            stream.Position = 0x3C;
            var peHeaderOffset = reader.ReadInt32();
            stream.Position = peHeaderOffset + 6;
            var numberOfSections = reader.ReadUInt16();
            reader.ReadUInt32(); // TimeDateStamp
            reader.ReadUInt32(); // PointerToSymbolTable
            reader.ReadUInt32(); // NumberOfSymbols
            var sizeOfOptionalHeader = reader.ReadUInt16();
            reader.ReadUInt16(); // Characteristics
            var section0StartPosition = (int) stream.Position + sizeOfOptionalHeader;

            for (var i = 0; i < numberOfSections; i++)
            {
                stream.Position = section0StartPosition + i * 40;
                reader.ReadBytes(8); // section name
                var virtualSize = reader.ReadUInt32();
                var virtualAddress = reader.ReadUInt32();
                writer.Write(virtualSize);    // SizeOfRawData   = VirtualSize
                writer.Write(virtualAddress); // PointerToRawData = VirtualAddress
            }
        }

        il2cppBytes = moduleBytes;

        // Scan for the metadata signature; trim to start at (signature - headerOffset) and repair the magic.
        var byteArray = moduleBytes;
        var index = Array.IndexOf(byteArray, metadataSignatureToScan[0]);
        while (index >= 0 && index <= byteArray.Length - metadataSignatureToScan.Length)
        {
            // index must be >= the header offset, else (index - metadataSignatureOffset) would be a
            // negative source index into Array.Copy; such an early match cannot be the real header.
            if (index >= metadataSignatureOffset &&
                byteArray.Skip(index).Take(metadataSignatureToScan.Length).SequenceEqual(metadataSignatureToScan))
            {
                var trimmedArray = new byte[byteArray.Length - index + metadataSignatureOffset];
                Array.Copy(byteArray, index - metadataSignatureOffset, trimmedArray, 0, trimmedArray.Length);
                if (magicToFix.Length > 0)
                    Array.Copy(magicToFix, 0, trimmedArray, 0, magicToFix.Length);
                byteArray = trimmedArray;
                break;
            }

            index = Array.IndexOf(byteArray, metadataSignatureToScan[0], index + 1);
        }

        metadataBytes = byteArray;
    }

    /// <summary>
    ///     Validates the dump result. If the signature did not match, <paramref name="metadataBytes" />
    ///     equals <paramref name="il2cppBytes" />; falls back to the on-disk metadata if present, otherwise
    ///     throws with remediation guidance (never blocks/crashes silently).
    /// </summary>
    public static void ValidateMetadata(ILogger logger, string metadataPath, byte[] il2cppBytes,
                                        ref byte[] metadataBytes)
    {
        if (!ReferenceEquals(il2cppBytes, metadataBytes))
            return;

        logger.LogWarning("global-metadata.dat is not embedded in GameAssembly.dll.");
        if (File.Exists(metadataPath))
        {
            logger.LogWarning("Found global-metadata.dat at the default path, using it instead.");
            metadataBytes = File.ReadAllBytes(metadataPath);
            return;
        }

        logger.LogError("global-metadata.dat could not be located: the embedded-metadata signature did not match " +
                        "this game build, and no global-metadata.dat exists at the default path ({Path}). " +
                        "Set [IL2CPP] MetadataSignatureToScan / MagicToFix / ObfuscatedMetadataHeaderOffset in " +
                        "BepInEx.cfg for the current game build, or place a valid global-metadata.dat there.",
                        metadataPath);
        throw new FileNotFoundException(
            "global-metadata.dat could not be located (embedded-metadata signature mismatch and no on-disk file). " +
            "See the log for remediation.", metadataPath);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}
