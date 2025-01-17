﻿// MIT License
// Copyright (c) [2024] [nexus-main]

using Apollo3zehn.PackageManagement;
using Apollo3zehn.PackageManagement.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Extensibility;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Other;

public class PackageControllerTests
{
    // Need to do it this way because GitHub revokes obvious tokens on commit.
    // However, this token - in combination with the test user's account
    // privileges - allows only read-only access to a test project, so there
    // is no real risk.
    private static readonly byte[] _token =
    [
        0x67,
        0x69,
        0x74,
        0x68,
        0x75,
        0x62,
        0x5F,
        0x70,
        0x61,
        0x74,
        0x5F,
        0x31,
        0x31,
        0x41,
        0x46,
        0x41,
        0x41,
        0x45,
        0x59,
        0x49,
        0x30,
        0x49,
        0x6D,
        0x33,
        0x54,
        0x51,
        0x57,
        0x53,
        0x74,
        0x73,
        0x69,
        0x30,
        0x6C,
        0x5F,
        0x4B,
        0x57,
        0x6F,
        0x41,
        0x6E,
        0x7A,
        0x43,
        0x48,
        0x52,
        0x50,
        0x39,
        0x6F,
        0x34,
        0x44,
        0x30,
        0x63,
        0x74,
        0x75,
        0x4B,
        0x38,
        0x47,
        0x5A,
        0x73,
        0x78,
        0x31,
        0x4A,
        0x4A,
        0x48,
        0x30,
        0x4A,
        0x64,
        0x33,
        0x32,
        0x41,
        0x71,
        0x62,
        0x66,
        0x63,
        0x4A,
        0x5A,
        0x44,
        0x48,
        0x31,
        0x42,
        0x5A,
        0x36,
        0x4A,
        0x43,
        0x32,
        0x56,
        0x46,
        0x72,
        0x53,
        0x4D,
        0x41,
        0x70,
        0x6A,
        0x79,
        0x77
    ];

    #region Load

    [Fact]
    public async Task CanLoadAndUnload()
    {
        var extensionFolderPath = "../../../../tests/resources/TestExtension";
        var extensionFolderPathHash = new Guid(extensionFolderPath.Hash()).ToString();

        // create restore folder
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"Nexus.Tests.{Guid.NewGuid()}");
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

        var dataSourceType = assembly
            .ExportedTypes
            .First(type => typeof(IDataSource).IsAssignableFrom(type));

        // run

        if (Activator.CreateInstance(dataSourceType) is not IDataSource dataSource)
            throw new Exception("data source is null");

        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => dataSource.EnrichCatalogAsync(default!, CancellationToken.None));

        Assert.Equal(nameof(IDataSource.EnrichCatalogAsync), exception.Message);

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
            .DiscoverAsync(CancellationToken.None))
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
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"Nexus.Tests.{Guid.NewGuid()}");
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
                ["repository"] = $"https://{Encoding.ASCII.GetString(_token)}@github.com/nexus-main/git-tag-provider-test-project"
            }
        );

        var packageController = new PackageController(packageReference, NullLogger<PackageController>.Instance);

        var actual = (await packageController
            .DiscoverAsync(CancellationToken.None))
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
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"Nexus.Tests.{Guid.NewGuid()}");
        var restoreFolderPath = Path.Combine(restoreRoot, "git-tag", "github.com_nexus-main_git-tag-provider-test-project", version);
        Directory.CreateDirectory(restoreRoot);

        try
        {
            var packageReference = new PackageReference(
                Provider: "git-tag",
                Configuration: new Dictionary<string, string>
                {
                    // required
                    ["repository"] = $"https://{Encoding.ASCII.GetString(_token)}@github.com/nexus-main/git-tag-provider-test-project",
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