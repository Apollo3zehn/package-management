// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using System.Text.Json;
using Apollo3zehn.PackageManagement.Core;
using Microsoft.Extensions.Logging;

namespace Apollo3zehn.PackageManagement.Services;

/// <summary>
/// An interface which defined interactions with package references.
/// </summary>
public interface IPackageService
{
    /// <summary>
    /// Puts a package reference.
    /// </summary>
    /// <param name="packageReference">The package reference.</param>
    Task<Guid> PutAsync(PackageReference packageReference);

    /// <summary>
    /// Tries to get the requested package reference. Returns null if the package reference does not exist.
    /// </summary>
    /// <param name="packageReferenceId">The package reference identifier.</param>
    Task<PackageReference?> GetAsync(Guid packageReferenceId);

    /// <summary>
    /// Tries to updated an existing package reference. Returns false if the package reference to update does not exist.
    /// </summary>
    /// <param name="packageReferenceId">The identifier of the package reference to update.</param>
    /// <param name="packageReference">The package reference which replaces the existing one.</param>
    Task<bool> TryUpdateAsync(Guid packageReferenceId, PackageReference packageReference);

    /// <summary>
    /// Deletes a package reference.
    /// </summary>
    /// <param name="packageReferenceId">The package reference ID.</param>
    /// <returns></returns>
    Task DeleteAsync(Guid packageReferenceId);

    /// <summary>
    /// Gets all package references.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PackageReference>> GetAllAsync();

    /// <summary>
    /// Gets all package versions.
    /// </summary>
    /// <param name="packageReferenceId">The package reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<string[]?> GetVersionsAsync(
        Guid packageReferenceId,
        CancellationToken cancellationToken);
}

internal class PackageService(
    IPackageManagementDatabaseService databaseService,
    ILoggerFactory loggerFactory
) : IPackageService
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    private Dictionary<Guid, PackageReference>? _cache;

    private readonly IPackageManagementDatabaseService _databaseService = databaseService;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public Task<Guid> PutAsync(PackageReference packageReference)
    {
        return InteractWithPackageReferenceMapAsync(packageReferenceMap =>
        {
            var id = Guid.NewGuid();

            packageReferenceMap[id] = packageReference;

            return id;
        }, saveChanges: true);
    }

    public Task<PackageReference?> GetAsync(Guid packageReferenceId)
    {
        return InteractWithPackageReferenceMapAsync(packageReferenceMap =>
        {
            var _ = packageReferenceMap.TryGetValue(packageReferenceId, out var packageReference);

            return packageReference;
        }, saveChanges: false);
    }

    public Task<bool> TryUpdateAsync(Guid packageReferenceId, PackageReference packageReference)
    {
        return InteractWithPackageReferenceMapAsync(packageReferenceMap =>
        {
            /* Proceed only if package reference already exists! 
             * We do not want package reference IDs being set from
             * outside.
             */
            if (packageReferenceMap.ContainsKey(packageReferenceId))
            {
                packageReferenceMap[packageReferenceId] = packageReference;
                return true;
            }

            else
            {
                return false;
            }
        }, saveChanges: true);
    }

    public Task DeleteAsync(Guid packageReferenceId)
    {
        return InteractWithPackageReferenceMapAsync<object?>(packageReferenceMap =>
        {
            var packageReferenceEntry = packageReferenceMap
                .FirstOrDefault(entry => entry.Key == packageReferenceId);

            packageReferenceMap.Remove(packageReferenceEntry.Key, out _);
            return default;
        }, saveChanges: true);
    }

    public Task<IReadOnlyDictionary<Guid, PackageReference>> GetAllAsync()
    {
        return InteractWithPackageReferenceMapAsync(
            packageReferenceMap => (IReadOnlyDictionary<Guid, PackageReference>)packageReferenceMap,
            saveChanges: false
        );
    }

    public async Task<string[]?> GetVersionsAsync(
        Guid packageReferenceId,
        CancellationToken cancellationToken)
    {
        var packageReference = await GetAsync(packageReferenceId);

        if (packageReference is null)
            return default;

        var controller = new PackageController(
            packageReference,
            _loggerFactory.CreateLogger<PackageController>());

        return await controller.GetVersionsAsync(cancellationToken);
    }

    private Dictionary<Guid, PackageReference> GetPackageReferenceMap()
    {
        if (_cache is null)
        {
            if (_databaseService.TryReadPackageReferenceMap(out var jsonString))
            {
                _cache = JsonSerializer.Deserialize<Dictionary<Guid, PackageReference>>(jsonString)
                    ?? throw new Exception("packageReferenceMap is null");
            }

            else
            {
                return new();
            }
        }

        return _cache;
    }

    private async Task<T> InteractWithPackageReferenceMapAsync<T>(
        Func<Dictionary<Guid, PackageReference>, T> func,
        bool saveChanges
    )
    {
        await _semaphoreSlim.WaitAsync().ConfigureAwait(false);

        try
        {
            var packageReferenceMap = GetPackageReferenceMap();
            var result = func(packageReferenceMap);

            if (saveChanges)
            {
                using var stream = _databaseService.WritePackageReferenceMap();
                JsonSerializerHelper.SerializeIndented(stream, packageReferenceMap);
            }

            return result;
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}
