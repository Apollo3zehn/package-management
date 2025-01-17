// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using System.Reflection;
using System.Runtime.Loader;

namespace Apollo3zehn.PackageManagement;

internal class PackageLoadContext(string entryDllPath)
    : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(entryDllPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        if (assemblyPath is not null)
            return LoadFromAssemblyPath(assemblyPath);

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        if (libraryPath is not null)
            return LoadUnmanagedDllFromPath(libraryPath);

        return IntPtr.Zero;
    }
}
