using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MortarSmapiBridge;

internal static class Files
{
    // restrict runs on the still-empty temp file, so its contents are never readable by others.
    internal static void AtomicWrite(string path, string contents, Action<string>? restrict = null)
    {
        string temp = path + ".tmp";
        try
        {
            if (restrict != null)
            {
                File.WriteAllText(temp, "");
                restrict(temp);
            }
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception)
            {
            }
            throw;
        }
    }

    internal static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            RestrictWindows(path);
            return;
        }

        if (chmod(path, 0x180) != 0)
            throw new IOException($"Could not restrict permissions on {path}.");
    }

    private static void RestrictWindows(string path)
    {
        string account = Environment.UserDomainName + "\\" + Environment.UserName;
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("/inheritance:r");
        start.ArgumentList.Add("/grant:r");
        start.ArgumentList.Add(account + ":(F)");
        using var process = Process.Start(start);
        if (process == null)
            throw new IOException($"Could not restrict permissions on {path}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            try
            {
                Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
            }
            throw new IOException($"Could not restrict permissions on {path}: timed out.");
        }
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new IOException($"Could not restrict permissions on {path}: {stderr.GetAwaiter().GetResult().Trim()}");
        _ = stdout.GetAwaiter().GetResult();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);
}
