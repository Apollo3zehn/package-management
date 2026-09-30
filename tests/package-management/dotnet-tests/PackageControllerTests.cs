// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using Apollo3zehn.PackageManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Other;

public class PackageControllerTests
{
    #region Load

    [Fact]
    public async Task CanLoadAndUnload()
    {
        // Arrange
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var version = "v0.1.0";

            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    ["repository"] = TestExtensionRepository.Repository,
                    ["tag"] = version,
                    ["entrypoint"] = "test-extension.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);
            var restoreFolderPath = await packageController.RestoreAsync(restoreRoot, CancellationToken.None);
            var fileToDelete = Path.Combine(restoreFolderPath, "test-extension.dll");

            // Act
            var weakReference = await Load_Run_and_Unload_Async(restoreRoot, fileToDelete, packageReference);

            // Assert
            for (int i = 0; weakReference.IsAlive && i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            File.Delete(fileToDelete);
        }
        finally
        {
            try
            {
                Directory.Delete(restoreRoot, recursive: true);
            }
            catch { }
        }
    }

    // https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> Load_Run_and_Unload_Async(
        string restoreRoot, string fileToDelete, PackageReference packageReference)
    {
        // load
        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);
        var assembly = await packageController.LoadAsync(restoreRoot, CancellationToken.None);

        var loggerType = assembly
            .ExportedTypes
            .First(type => typeof(ILogger).IsAssignableFrom(type));

        // run
        if (Activator.CreateInstance(loggerType) is not ILogger logger)
            throw new Exception("logger is null");

        // delete should fail
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Throws<UnauthorizedAccessException>(() => File.Delete(fileToDelete));

        // unload
        var weakReference = packageController.Unload();

        return weakReference;
    }

    #endregion

    #region Provider: git_tag local

    [Fact]
    public async Task CanGetVersions_git_tag_local()
    {
        // Arrange
        var expected = new[]
        {
            "v2.0.0",
            "v1.1.1",
            "v1.0.1",
            "v1.0.0-beta2+12347",
            "v1.0.0-beta1+12346",
            "v1.0.0-alpha1+12345",
            "v0.1.0"
        };

        var packageReference = new PackageReference(
            Provider: "git-tag",
            Configuration: new Dictionary<string, string>
            {
                ["repository"] = TestExtensionRepository.Repository,
            }
        );

        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

        // Act
        var actual = (await packageController
            .GetVersionsAsync(CancellationToken.None))
            .ToArray();

        // Assert
        Assert.Equal(expected.Length, actual.Length);

        foreach (var (expectedItem, actualItem) in expected.Zip(actual))
        {
            Assert.Equal(expectedItem, actualItem);
        }
    }

    [Fact]
    public async Task CanRestore_git_tag_local()
    {
        // Arrange
        var version = "v0.1.0";

        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    ["repository"] = TestExtensionRepository.Repository,
                    ["tag"] = version,
                    ["entrypoint"] = "test-extension.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

            // Act
            var restoreFolderPath = await packageController.RestoreAsync(restoreRoot, CancellationToken.None);

            // Assert
            var expectedFilePath = Path.Combine(restoreFolderPath, "test-extension.deps.json");

            Assert.True(File.Exists(expectedFilePath));
        }
        finally
        {
            Directory.Delete(restoreRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CanStampAssemblyVersion_git_tag_local()
    {
        // Arrange
        var version = "v0.1.0";

        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    ["repository"] = TestExtensionRepository.Repository,
                    ["tag"] = version,
                    ["entrypoint"] = "test-extension.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

            // Act
            var assembly = await packageController.LoadAsync(restoreRoot, CancellationToken.None);

            try
            {
                var informationalVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;

                // Assert
                Assert.StartsWith("0.1.0", informationalVersion);
            }
            finally
            {
                var weakReference = packageController.Unload();

                for (int i = 0; weakReference.IsAlive && i < 10; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(restoreRoot, recursive: true);
            }
            catch { }
        }
    }

    #endregion

    #region Provider: git_tag

    [Fact]
    public async Task CanGetVersions_git_tag()
    {
        // Arrange
        var expected = new[]
        {
            "v2.0.0-beta.1",
            "v2.0.0",
            "v1.1.1",
            "v1.0.1",
            "v1.0.0-beta2+12347",
            "v1.0.0-beta1+12346",
            "v1.0.0-alpha1+12345",
            "v0.1.0"
        };

        var packageReference = new PackageReference(
            Provider: "git-tag",
            Configuration: new Dictionary<string, string>
            {
                ["repository"] = $"https://github.com/Apollo3zehn/git-tags-provider-test-project"
            }
        );

        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

        // Act
        var actual = (await packageController
            .GetVersionsAsync(CancellationToken.None))
            .ToArray();

        // Assert
        Assert.Equal(expected.Length, actual.Length);

        foreach (var (expectedItem, actualItem) in expected.Zip(actual))
        {
            Assert.Equal(expectedItem, actualItem);
        }
    }

    [Fact]
    public async Task CanRestore_git_tag()
    {
        // Arrange
        var version = "v2.0.0-beta.1";

        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        var restoreFolderPath = Path.Combine(restoreRoot, "git-tag", "https_github.com_Apollo3zehn_git-tags-provider-test-project", version);

        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    ["repository"] = $"https://github.com/Apollo3zehn/git-tags-provider-test-project",
                    ["tag"] = version,
                    ["entrypoint"] = "git-tags-provider-test-project.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

            // Act
            await packageController.RestoreAsync(restoreRoot, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(restoreFolderPath, "git-tags-provider-test-project.deps.json")));
        }
        finally
        {
            Directory.Delete(restoreRoot, recursive: true);
        }
    }

    #endregion
}
