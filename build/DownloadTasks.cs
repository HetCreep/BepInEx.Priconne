using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.IO;
using Spectre.Console;
using Path = System.IO.Path;

static class DownloadTasks
{
    public static void DownloadFiles(this ICakeContext ctx,
                                     string name,
                                     params (string Name, string Url, FilePath Destination)[] files)
    {
        AnsiConsole.Progress().Start(pCtx =>
        {
            Task.WaitAll(files
                         .Select(t => DownloadFile(pCtx, t.Name, t.Url, t.Destination))
                         .ToArray());
        });
    }

    public static void DownloadZipFiles(this ICakeContext ctx,
                                        string name,
                                        params (string Name, string Url, DirectoryPath Destination)[] files)
    {
        AnsiConsole.Progress().Start(pCtx =>
        {
            Task.WaitAll(files.Select(async t =>
            {
                var zipFilePath = $"{t.Destination}_tmp.zip";
                await DownloadFile(pCtx, t.Name, t.Url, zipFilePath);
                await UnzipFile(pCtx, t.Name, zipFilePath, t.Destination);
                File.Delete(zipFilePath!);
            }).ToArray());
        });
    }

    static async Task DownloadFile(ProgressContext pCtx, string name, string url, FilePath destination)
    {
        using var client = new HttpClient();
        await using var fs = File.Create(destination.FullPath);
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to download {name}");
        await using var stream = await response.Content.ReadAsStreamAsync();
        var bar = pCtx.AddTask($"Downloading {name}", maxValue: (int) (response.Content.Headers.ContentLength ?? 1));

        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read));
            bar.Increment(read);
        }
    }

    static async Task UnzipFile(ProgressContext pCtx, string name, FilePath zipFile, DirectoryPath destination)
    {
        using var zip = ZipFile.OpenRead(zipFile.FullPath);
        var bar = pCtx.AddTask($"Extracting {name}", maxValue: (int) zip.Entries.Select(e => e.Length).Sum());

        foreach (var entry in zip.Entries)
        {
            // Combine first (keeps the trailing slash that marks a directory entry).
            var combined = Path.Combine(destination.FullPath, entry.FullName);
            // Zip Slip guard (CWE-22): reject entries whose resolved path escapes the destination.
            var resolvedPath = Path.GetFullPath(combined);
            var destRoot = Path.GetFullPath(destination.FullPath) + Path.DirectorySeparatorChar;
            if (!resolvedPath.StartsWith(destRoot, StringComparison.Ordinal))
                throw new IOException($"Blocked Zip Slip: '{entry.FullName}' escapes the extraction directory.");
            var fileName = Path.GetFileName(combined);
            if (string.IsNullOrEmpty(fileName))
            {
                Directory.CreateDirectory(resolvedPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath) ?? throw new InvalidOperationException());
            await using var fs = File.Create(resolvedPath);
            await using var stream = entry.Open();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                bar.Increment(read);
            }
        }
    }
}
