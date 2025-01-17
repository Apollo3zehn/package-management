using System.Diagnostics;
using Microsoft.Extensions.Options;
using Apollo3zehn.PackageManagement.Services;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up MVC services in an <see cref="IServiceCollection" />.
/// </summary>
public static class MvcServiceCollectionExtensions
{
    /// <summary>
    /// Adds services required for the package management.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddPackageManagement<T>(this IServiceCollection services)
        where T : class
    {
        return services
            .Configure<PackageManagementPathsOptions>(_ => { })
            .AddSingleton<IPackageService, PackageService>()
            .AddSingleton<IPackageManagementDatabaseService, PackageManagementDatabaseService>()
            .AddSingleton<IExtensionHive<T>, ExtensionHive<T>>()
            .AddSingleton<IPackageManagementPathsOptions>(
                serviceProvider => serviceProvider.GetRequiredService<IOptions<PackageManagementPathsOptions>>().Value);
    }
}