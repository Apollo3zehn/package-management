using System.Diagnostics;

namespace Other;

internal static class TestExtensionRepository
{
    private static readonly object Lock = new();

    private static readonly string[] Tags =
    [
        "v0.1.0",
        "v1.0.0-alpha1+12345",
        "v1.0.0-beta1+12346",
        "v1.0.0-beta2+12347",
        "v1.0.1",
        "v1.1.1",
        "v2.0.0",
    ];

    public static string Repository
    {
        get
        {
            EnsureRepository();
            return new Uri(Path).AbsoluteUri;
        }
    }

    private static string Path => System.IO.Path.GetFullPath("../../../../tests/resources/test-extension");

    private static void EnsureRepository()
    {
        lock (Lock)
        {
            if (Directory.Exists(System.IO.Path.Combine(Path, ".git")))
                return;

            RunGit("init");
            RunGit("add", ".");
            RunGit(
                "-c", "user.name=Package Management Tests",
                "-c", "user.email=package-management-tests@example.invalid",
                "commit", "-m", "Initial test extension");

            foreach (var tag in Tags)
            {
                RunGit("tag", tag);
            }
        }
    }

    private static void RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = "git",
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new Exception("Process is null.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Unable to run git {string.Join(' ', arguments)}. Reason: {process.StandardError.ReadToEnd()}");
        }
    }
}
