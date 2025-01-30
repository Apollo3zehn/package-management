// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using Apollo3zehn.PackageManagement;
using Apollo3zehn.PackageManagement.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Services;

public class ExtensionHiveTests
{
    [Fact]
    public async Task CanInstantiateExtensions()
    {
        var extensionFolderPath = "../../../../tests/resources/test-extension";

        // create restore folder
        var restoreRoot = Path.Combine(Path.GetTempPath(), $"PackageManagement.Tests.{Guid.NewGuid()}");
        Directory.CreateDirectory(restoreRoot);

        try
        {
            // load packages
            var pathsOptions = Mock.Of<IPackageManagementPathsOptions>();

            Mock.Get(pathsOptions)
                .SetupGet(pathsOptions => pathsOptions.Packages)
                .Returns(restoreRoot);

            var loggerFactory = Mock.Of<ILoggerFactory>();

            Mock.Get(loggerFactory)
                .Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>()))
                .Returns(NullLogger.Instance);

            var hive = new ExtensionHive<ILogger>(pathsOptions, NullLogger<ExtensionHive<ILogger>>.Instance, loggerFactory);
            var version = "v0.1.0";

            var packageReference = new PackageReference(
                Provider: "local",
                Configuration: new Dictionary<string, string>
                {
                    ["path"] = extensionFolderPath,
                    ["version"] = version,
                    ["entrypoint"] = "test-extension.csproj"
                }
            );

            var packageReferenceMap = new Dictionary<Guid, PackageReference>
            {
                [Guid.NewGuid()] = packageReference
            };

            await hive.LoadPackagesAsync(packageReferenceMap, new Progress<double>(), CancellationToken.None);

            // instantiate
            var type = hive.GetExtensionType("Foo.MyLogger");
            var logger = (ILogger)Activator.CreateInstance(type)!;

            Assert.NotNull(logger);
            Assert.False(logger.IsEnabled(LogLevel.Trace));
            Assert.True(logger.IsEnabled(LogLevel.Debug));
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
}