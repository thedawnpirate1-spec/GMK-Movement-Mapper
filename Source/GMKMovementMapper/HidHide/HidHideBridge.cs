using System.Diagnostics;
using System.Text.Json;

namespace GMKMovementMapper.HidHide;

public sealed class HidHideBridge
{
    private static readonly string[] CandidatePaths =
    [
        @"C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe",
        @"C:\Program Files\Nefarius Software Solutions e.U.\HidHide\x64\HidHideCLI.exe"
    ];

    private static string? CliPath => CandidatePaths.FirstOrDefault(File.Exists);

    public bool IsInstalled
    {
        get
        {
            return CliPath is not null;
        }
    }

    public IReadOnlyList<string> FindGmkInstanceIds()
    {
        var result = new List<string>();
        var output = RunCli("--dev-gaming");
        using var document = JsonDocument.Parse(output);
        foreach (var group in document.RootElement.EnumerateArray())
        {
            if (!group.TryGetProperty("devices", out var devices)) continue;
            foreach (var device in devices.EnumerateArray())
            {
                if (device.TryGetProperty("deviceInstancePath", out var value))
                    result.Add(value.GetString() ?? string.Empty);
            }
        }
        result.RemoveAll(string.IsNullOrWhiteSpace);
        return result;
    }

    public void ConfigureForMapper(string instanceId)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The mapper executable path could not be determined.");

        // These are the documented HidHideCLI operations. They control the existing
        // signed driver and do not require the configuration GUI to remain open.
        RunCli("--app-reg", executable);
        RunCli("--dev-hide", instanceId);
        RunCli("--cloak-on");
    }

    public void SetActive(bool active) => RunCli(active ? "--cloak-on" : "--cloak-off");

    private static string RunCli(params string[] arguments)
    {
        var path = CliPath ?? throw new FileNotFoundException("HidHideCLI.exe was not found.");
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("HidHideCLI could not be started.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        return stdout;
    }
}
