// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using Apollo3zehn.PackageManagement;
using Apollo3zehn.PackageManagement.Core;
using Apollo3zehn.PackageManagement.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Services;

public class PackageServiceTests
{
    delegate bool GobbleReturns(out string? pipelineMap);

    [Fact]
    public async Task CanCreatePackageReference()
    {
        // Arrange
        var filePath = Path.GetTempFileName();
        var packageService = GetPackageService(filePath, []);

        var packageReference = new PackageReference(
            Provider: "foo",
            Configuration: default!
        );

        // Act
        var expectedId = await packageService.PutAsync(packageReference);

        // Assert
        var jsonString = File.ReadAllText(filePath);

        var actualPackageReferenceMap = JsonSerializer.Deserialize<Dictionary<Guid, PackageReference>>(
            jsonString,
            JsonSerializerOptions.Web
        )!;

        var entry = Assert.Single(actualPackageReferenceMap);

        Assert.Equal(expectedId, entry.Key);
        Assert.Equal("foo", entry.Value.Provider);
        Assert.Null(entry.Value.Configuration);
    }

    [Fact]
    public async Task CanGetPackageReference()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var packageReferenceMap = new Dictionary<Guid, PackageReference>()
        {
            [id1] = new PackageReference(
                Provider: "foo",
                Configuration: default!
            ),
            [id2] = new PackageReference(
                Provider: "bar",
                Configuration: default!
            )
        };

        var packageService = GetPackageService(default!, packageReferenceMap);

        // Act
        var actual = await packageService.GetAsync(id2);

        // Assert
        Assert.Equal(
            expected: JsonSerializer.Serialize(actual),
            actual: JsonSerializer.Serialize(packageReferenceMap[id2])
        );
    }

    [Fact]
    public async Task CanTryUpdatePackageReference()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var packageReferenceMap = new Dictionary<Guid, PackageReference>()
        {
            [id1] = new PackageReference(
                Provider: "foo",
                Configuration: default!
            ),
            [id2] = new PackageReference(
                Provider: "bar",
                Configuration: default!
            )
        };

        var filePath = Path.GetTempFileName();
        var packageService = GetPackageService(filePath, packageReferenceMap);

        var newPackageReference = new PackageReference(
            Provider: "baz",
            Configuration: default!
        );

        var expected = packageReferenceMap
            .ToDictionary(x => x.Key, x => x.Value);

        expected[id1] = newPackageReference;

        // Act
        var success = await packageService.TryUpdateAsync(id1, newPackageReference);

        // Assert
        Assert.True(success);

        var actual = JsonSerializer.Deserialize<Dictionary<Guid, PackageReference>>(
            File.ReadAllText(filePath),
            JsonSerializerOptions.Web
        );

        Assert.Equivalent(expected, actual, strict: true);
    }

    [Fact]
    public async Task CanDeletePackage()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var packageReferencesMap = new Dictionary<Guid, PackageReference>()
        {
            [id1] = new PackageReference(
                Provider: "foo",
                Configuration: default!
            ),
            [id2] = new PackageReference(
                Provider: "bar",
                Configuration: default!
            )
        };

        var filePath = Path.GetTempFileName();
        var packageService = GetPackageService(filePath, packageReferencesMap);

        // Act
        await packageService.DeleteAsync(id1);

        // Assert
        packageReferencesMap.Remove(id1);
        var expected = JsonSerializerHelper.SerializeIndented(packageReferencesMap);
        var actual = File.ReadAllText(filePath);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CanGetAllPackages()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var packageReferencesMap = new Dictionary<Guid, PackageReference>()
        {
            [id1] = new PackageReference(
                Provider: "foo",
                Configuration: default!
            ),
            [id2] = new PackageReference(
                Provider: "bar",
                Configuration: default!
            )
        };

        var filePath = Path.GetTempFileName();
        var packageService = GetPackageService(filePath, packageReferencesMap);

        // Act
        var actualPackageMap = await packageService.GetAllAsync();

        // Assert
        var expected = JsonSerializerHelper.SerializeIndented(packageReferencesMap.OrderBy(current => current.Key));
        var actual = JsonSerializerHelper.SerializeIndented(actualPackageMap.OrderBy(current => current.Key));

        Assert.Equal(expected, actual);
    }

    private static IPackageService GetPackageService(
        string filePath,
        Dictionary<Guid, PackageReference> packageReferenceMap)
    {
        var databaseService = Mock.Of<IPackageManagementDatabaseService>();

        Mock.Get(databaseService)
            .Setup(databaseService => databaseService.TryReadPackageReferenceMap(out It.Ref<string?>.IsAny))
            .Returns(new GobbleReturns((out string? packageReferenceMapString) =>
            {
                packageReferenceMapString = JsonSerializer.Serialize(packageReferenceMap);
                return true;
            }));

        Mock.Get(databaseService)
            .Setup(databaseService => databaseService.WritePackageReferenceMap())
            .Returns(() => File.OpenWrite(filePath));

        var loggerFactory = new LoggerFactory();
        var packageService = new PackageService(databaseService, loggerFactory);

        return packageService;
    }
}