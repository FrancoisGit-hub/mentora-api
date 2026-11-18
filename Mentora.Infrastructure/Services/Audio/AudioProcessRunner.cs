using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Mentora.Infrastructure.Services.Audio;

/// <summary>
/// Lot 13.3. Result of one ffmpeg or ffprobe run.
/// </summary>
/// <param name="Started">False when the executable could not be started (missing, not executable).</param>
/// <param name="ExitCode">Exit code, null when not started.</param>
/// <param name="Stdout">Buffer holding standard output; only the first <paramref name="StdoutLength"/> bytes are valid.</param>
/// <param name="StdoutLength">Number of valid bytes in <paramref name="Stdout"/>.</param>
/// <param name="StdoutCapped">True when the output went beyond the cap: reading stopped and the process was killed.</param>
/// <param name="Stderr">First <see cref="AudioProcessRunner.StderrKeepChars"/> characters of standard error.</param>
internal sealed record AudioProcessResult(
    bool Started,
    int? ExitCode,
    byte[] Stdout,
    int StdoutLength,
    bool StdoutCapped,
    string Stderr)
{
    /// <summary>Result of a process that could not be started.</summary>
    public static AudioProcessResult NotStarted { get; } = new(false, null, [], 0, false, "");
}

/// <summary>
/// Lot 13.3. Runs ffmpeg or ffprobe as a child process: no shell, arguments passed one by one through
/// <see cref="ProcessStartInfo.ArgumentList"/>, standard output read into a capped buffer, standard error
/// read concurrently and kept to its first 4 KB, process tree killed on cancellation or beyond the cap,
/// process disposed in all paths.
/// </summary>
internal static class AudioProcessRunner
{
    /// <summary>Number of standard error characters kept.</summary>
    public const int StderrKeepChars = 4096;

    /// <summary>
    /// Runs the executable. Throws <see cref="OperationCanceledException"/> (after killing the process
    /// tree) when <paramref name="ct"/> is cancelled.
    /// </summary>
    public static async Task<AudioProcessResult> RunAsync(
        string fileName, IReadOnlyList<string> arguments, int stdoutCap, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return AudioProcessResult.NotStarted;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return AudioProcessResult.NotStarted;
        }

        try
        {
            // Killing the process closes its pipes, so the reads below end even on a cancellation.
            await using var registration = ct.Register(() => KillTree(process));

            var stderrTask = ReadStderrAsync(process.StandardError);

            var buffer = new byte[stdoutCap];
            var length = 0;
            var capped = false;
            var chunk = new byte[16 * 1024];
            var stdout = process.StandardOutput.BaseStream;
            while (true)
            {
                int read;
                try
                {
                    read = await stdout.ReadAsync(chunk, CancellationToken.None);
                }
                catch (IOException)
                {
                    break; // Pipe closed by a kill.
                }
                if (read == 0)
                    break;

                var room = stdoutCap - length;
                if (read > room)
                {
                    Buffer.BlockCopy(chunk, 0, buffer, length, room);
                    length += room;
                    capped = true;
                    KillTree(process);
                    break;
                }
                Buffer.BlockCopy(chunk, 0, buffer, length, read);
                length += read;
            }

            await process.WaitForExitAsync(CancellationToken.None);
            var stderr = await stderrTask;
            ct.ThrowIfCancellationRequested();

            return new AudioProcessResult(true, process.ExitCode, buffer, length, capped, stderr);
        }
        finally
        {
            KillTree(process);
        }
    }

    private static async Task<string> ReadStderrAsync(StreamReader reader)
    {
        var kept = new StringBuilder();
        var chars = new char[1024];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(chars, 0, chars.Length)) > 0)
            {
                var room = StderrKeepChars - kept.Length;
                if (room > 0)
                    kept.Append(chars, 0, Math.Min(room, read));
                // Beyond the limit the stream is still drained so the process never blocks on a full pipe.
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Pipe closed by a kill: keep what was read.
        }
        return kept.ToString();
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already exited or never started.
        }
    }
}
