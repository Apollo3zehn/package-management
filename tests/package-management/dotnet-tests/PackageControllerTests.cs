﻿// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using Apollo3zehn.PackageManagement;
using Apollo3zehn.PackageManagement.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Other;

public class PackageControllerTests
{
    #region Load

    [Fact]
    public async Task CanLoadAndUnload()
    {
        var extensionFolderPath = "../../../../tests/resources/TestExtension";
        var extensionFolderPathHash = new Guid(extensionFolderPath.Hash()).ToString();

        // create restore folder
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var version = "v0.1.0";

            var packageReference = new PackageReference(
                Provider: "local",
                Configuration: new Dictionary<string, string>
                {
                    // required
                    ["path"] = extensionFolderPath,
                    ["version"] = version,
                    ["csproj"] = "TestExtension.csproj"
                }
            );

            var fileToDelete = Path.Combine(restoreRoot, "local", extensionFolderPathHash, version, "TestExtension.dll");
            var weakReference = await Load_Run_and_Unload_Async(restoreRoot, fileToDelete, packageReference);

            for (int i = 0; weakReference.IsAlive && i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // try to delete file
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

    #region Provider: local

    [Fact]
    public async Task CanDiscover_local()
    {
        var expected = new[]
        {
            "v2.0.0 postfix",
            "v1.1.1 postfix",
            "v1.0.1 postfix",
            "v1.0.0-beta2+12347 postfix",
            "v1.0.0-beta1+12346 postfix",
            "v1.0.0-alpha1+12345 postfix",
            "v0.1.0"
        };

        var packageReference = new PackageReference(
            Provider: "local",
            Configuration: new Dictionary<string, string>
            {
                ["path"] = "../../../../tests/resources/TestExtension",
            }
        );

        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

        var actual = (await packageController
            .GetVersionsAsync(CancellationToken.None))
            .ToArray();

        Assert.Equal(expected.Length, actual.Length);

        foreach (var (expectedItem, actualItem) in expected.Zip(actual))
        {
            Assert.Equal(expectedItem, actualItem);
        }
    }

    [Fact]
    public async Task CanRestore_local()
    {
        var version = "v0.1.0";
        var extensionFolderPath = "../../../../tests/resources/TestExtension";
        var extensionFolderPathHash = new Guid(extensionFolderPath.Hash()).ToString();

        // create restore folder
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        var restoreFolderPath = Path.Combine(restoreRoot, "local", extensionFolderPathHash, version);
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "local",
                Configuration: new Dictionary<string, string>
                {
                    // required
                    ["path"] = extensionFolderPath,
                    ["version"] = version,
                    ["csproj"] = "TestExtension.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);
            await packageController.RestoreAsync(restoreRoot, CancellationToken.None);
            var expectedFilePath = Path.Combine(restoreFolderPath, "TestExtension.deps.json");

            Assert.True(File.Exists(expectedFilePath));
        }
        finally
        {
            Directory.Delete(restoreRoot, recursive: true);
        }
    }

    #endregion

    #region Provider: git_tag

    [Fact]
    public async Task CanDiscover_git_tag()
    {
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
                // required
                ["repository"] = $"https://github.com/Apollo3zehn/git-tags-provider-test-project"
            }
        );

        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

        var actual = (await packageController
            .GetVersionsAsync(CancellationToken.None))
            .ToArray();

        Assert.Equal(expected.Length, actual.Length);

        foreach (var (expectedItem, actualItem) in expected.Zip(actual))
        {
            Assert.Equal(expectedItem, actualItem);
        }
    }

    [Fact]
    public async Task CanRestore_git_tag()
    {
        var version = "v2.0.0-beta.1";

        // create restore folder
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        var restoreFolderPath = Path.Combine(restoreRoot, "git-tag", "github.com_apollo3zehn_git-tags-provider-test-project", version);
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    // required
                    ["repository"] = $"https://github.com/Apollo3zehn/git-tags-provider-test-project",
                    ["tag"] = version,
                    ["csproj"] = "git-tags-provider-test-project.csproj"
                }
            );

            var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);
            await packageController.RestoreAsync(restoreRoot, CancellationToken.None);

            Assert.True(File.Exists(Path.Combine(restoreFolderPath, "git-tags-provider-test-project.deps.json")));
            Assert.True(File.Exists(Path.Combine(restoreFolderPath, "git-tags-provider-test-project.dll")));
            Assert.True(File.Exists(Path.Combine(restoreFolderPath, "git-tags-provider-test-project.pdb")));
        }
        finally
        {
            Directory.Delete(restoreRoot, recursive: true);
        }
    }

    #endregion
}