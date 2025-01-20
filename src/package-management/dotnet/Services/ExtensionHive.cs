// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Apollo3zehn.PackageManagement.Services;

/// <summary>
/// An interface which defines method to interact with the extension hive.
/// </summary>
public interface IExtensionHive<T> where T : class
{
    /// <summary>
    /// Gets all extensions of the specified type.
    /// </summary>
    IEnumerable<Type> GetExtensions();

    /// <summary>
    /// Gets the package reference ID for the specified type name.
    /// </summary>
    /// <param name="fullName">The type name.</param>
    Guid GetPackageReferenceId(string fullName);

    /// <summary>
    /// Create a new extension instance.
    /// </summary>
    /// <param name="fullName">The type name.</param>
    T GetInstance(string fullName);

    /// <summary>
    /// Loads the map of packages.
    /// </summary>
    /// <param name="packageReferenceMap">The packages map.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    Task LoadPackagesAsync(
        IReadOnlyDictionary<Guid, PackageReference> packageReferenceMap,
        IProgress<double> progress,
        CancellationToken cancellationToken);
}

internal class ExtensionHive<T>(
    IPackageManagementPathsOptions pathsOptions,
    ILogger<ExtensionHive<T>> logger,
    ILoggerFactory loggerFactory) : IExtensionHive<T>
    where T : class
{
    private readonly ILogger<ExtensionHive<T>> _logger = logger;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    private readonly IPackageManagementPathsOptions _pathsOptions = pathsOptions;

    private Dictionary<Guid, (PackageController, ReadOnlyCollection<Type>)>? _packageControllerMap = default!;

    public async Task LoadPackagesAsync(
        IReadOnlyDictionary<Guid, PackageReference> packageReferenceMap,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        // Clean up
        if (_packageControllerMap is not null)
        {
            _logger.LogDebug("Unload previously loaded packages");

            foreach (var (_, (controller, _)) in _packageControllerMap)
            {
                controller.Unload();
            }

            _packageControllerMap = default;
        }

        var builtInPackageReference = new PackageReference(
            Provider: PackageController.BUILTIN_PROVIDER,
            Configuration: []
        );

        // Build new
        var packageControllerMap = new Dictionary<Guid, (PackageController, ReadOnlyCollection<Type>)>();
        var currentCount = 0;
        var totalCount = packageReferenceMap.Count();

        foreach (var (id, packageReference) in packageReferenceMap
            .Concat([new KeyValuePair<Guid, PackageReference>(
                PackageController.BUILTIN_ID,
                builtInPackageReference
            )]))
        {
            var packageController = new PackageController(packageReference, _loggerFactory.CreateLogger<PackageController>());
            using var scope = _logger.BeginScope(packageReference.Configuration.ToDictionary(entry => entry.Key, entry => (object)entry.Value));

            try
            {
                _logger.LogDebug("Load package");
                var assembly = await packageController.LoadAsync(_pathsOptions.Packages, cancellationToken);

                /* Currently, only the directly referenced assembly is being searched for extensions. When this
                 * behavior should change, it is important to think about the consequences: What should happen when
                 * an extension is referenced as usual but at the same time it serves as a base class extension in
                 * other packages. If all assemblies in that package are being scanned, the original extension would
                 * be found twice.
                 */
                var types = ScanAssembly(assembly, packageReference.Provider == PackageController.BUILTIN_PROVIDER
                    ? assembly.DefinedTypes
                    : assembly.ExportedTypes);

                packageControllerMap[id] = (packageController, types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loading package failed");
            }

            currentCount++;
            progress.Report(currentCount / (double)totalCount);
        }

        _packageControllerMap = packageControllerMap;
    }

    public IEnumerable<Type> GetExtensions()
    {
        if (_packageControllerMap is null)
            return Enumerable.Empty<Type>();

        else
            return _packageControllerMap.SelectMany(entry => entry.Value.Item2);
    }

    public Guid GetPackageReferenceId(string fullName)
    {
        if (!TryGetTypeInfo(fullName, out var packageReferenceId, out var _, out var _))
            throw new Exception($"Could not find extension {fullName} of type {typeof(T).FullName}.");

        return packageReferenceId;
    }

    public T GetInstance(string fullName)
    {
        if (!TryGetTypeInfo(fullName, out var _, out var _, out var type))
            throw new Exception($"Could not find extension {fullName} of type {typeof(T).FullName}.");

        _logger.LogDebug("Instantiate extension {ExtensionType}", fullName);

        var instance = (T)(Activator.CreateInstance(type) ?? throw new Exception("instance is null"));

        return instance;
    }

    private bool TryGetTypeInfo(
        string fullName,
        [NotNullWhen(true)] out Guid packageReferenceId,
        [NotNullWhen(true)] out PackageController? packageController,
        [NotNullWhen(true)] out Type? type)
    {
        packageReferenceId = default;
        packageController = default;
        type = default;

        if (_packageControllerMap is null)
            return false;

        IEnumerable<(Guid Id, PackageController Controller, Type Type)> typeInfos = _packageControllerMap
            .SelectMany(entry => entry.Value.Item2.Select(type => (entry.Key, entry.Value.Item1, type)));

        (packageReferenceId, packageController, type) = typeInfos
            .Where(typeInfo => typeInfo.Type.FullName == fullName)
            .FirstOrDefault();

        if (type is null)
            return false;

        return true;
    }

    private ReadOnlyCollection<Type> ScanAssembly(Assembly assembly, IEnumerable<Type> types)
    {
        var foundTypes = types
            .Where(type =>
            {
                var isClass = type.IsClass;
                var isInstantiatable = !type.IsAbstract;
                var isCorrectType = typeof(T).IsAssignableFrom(type);

                if (isClass && isInstantiatable && isCorrectType)
                {
                    var hasParameterlessConstructor = type.GetConstructor(Type.EmptyTypes) is not null;

                    if (!hasParameterlessConstructor)
                        _logger.LogWarning("Type {TypeName} from assembly {AssemblyName} has no parameterless constructor", type.FullName, assembly.FullName);

                    return hasParameterlessConstructor;
                }

                return false;
            })
            .ToList()
            .AsReadOnly();

        return foundTypes;
    }
}
