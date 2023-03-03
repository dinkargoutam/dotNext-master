using System.IO.Compression;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft;

using FileReader = IO.FileReader;

public partial class PersistentState
{
    private readonly CompressionLevel backupCompression;

    /// <summary>
    /// Creates backup of this audit trail.
    /// </summary>
    /// <param name="output">The stream used to store backup.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task CreateBackupAsync(Stream output, CancellationToken token = default)
    {
        ZipArchive? archive = null;
        await syncRoot.AcquireAsync(LockType.StrongReadLock, token).ConfigureAwait(false);
        try
        {
            archive = new(output, ZipArchiveMode.Create, leaveOpen: true);
            foreach (var file in Location.EnumerateFiles())
            {
                var entry = archive.CreateEntry(file.Name, backupCompression);
                entry.LastWriteTime = file.LastWriteTime;
                SafeFileHandle? sourceHandle = null;
                FileReader? source = null;
                Stream? destination = null;
                try
                {
                    sourceHandle = File.OpenHandle(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    source = new(sourceHandle, bufferSize: 8096);
                    destination = entry.Open();
                    await source.CopyToAsync(destination, token).ConfigureAwait(false);
                }
                finally
                {
                    source?.Dispose();
                    sourceHandle?.Dispose();

                    if (destination is not null)
                        await destination.DisposeAsync().ConfigureAwait(false);
                }
            }

            await output.FlushAsync(token).ConfigureAwait(false);
        }
        finally
        {
            syncRoot.Release(LockType.StrongReadLock);
            archive?.Dispose();
        }
    }

    /// <summary>
    /// Restores persistent state from backup.
    /// </summary>
    /// <remarks>
    /// All files within destination directory will be deleted
    /// permanently.
    /// </remarks>
    /// <param name="backup">The stream containing backup.</param>
    /// <param name="destination">The destination directory.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task RestoreFromBackupAsync(Stream backup, DirectoryInfo destination, CancellationToken token = default)
    {
        // cleanup directory
        foreach (var file in destination.EnumerateFiles())
            file.Delete();

        // extract files from archive
        using var archive = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            var fs = new FileStream(Path.Combine(destination.FullName, entry.Name), FileMode.Create, FileAccess.Write, FileShare.None, 1024, useAsync: true);
            var entryStream = entry.Open();
            await using (fs.ConfigureAwait(false))
            await using (entryStream.ConfigureAwait(false))
            {
                await entryStream.CopyToAsync(fs, token).ConfigureAwait(false);
            }
        }
    }
}