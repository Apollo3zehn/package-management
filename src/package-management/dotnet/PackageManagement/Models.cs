// MIT License
// Copyright (c) [2024] [Apollo3zehn]

namespace Apollo3zehn.PackageManagement;

/// <summary>
/// A package reference.
/// </summary>
/// <remarks>
/// For providers that publish a .NET project, the selected version or tag is also used for build stamping.
/// The first whitespace-delimited token is used and a leading <c>v</c> is removed before passing it to MSBuild
/// as <c>Version</c>. Supported selector examples include <c>v2.0.0</c>, <c>2.0.0</c>,
/// <c>v2.0.0-beta.1</c>, <c>2.0.0+12345</c>, and <c>v2.0.0 release</c>.
/// If the resulting build version is not a valid NuGet version, the project is published without version stamping.
/// </remarks>
/// <param name="Provider">The provider which loads the package.</param>
/// <param name="Configuration">The configuration of the package reference.</param>
public record PackageReference(
    string Provider,
    Dictionary<string, string> Configuration);
