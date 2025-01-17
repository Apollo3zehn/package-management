// MIT License
// Copyright (c) [2024] [Apollo3zehn]

namespace Apollo3zehn.PackageManagement;

/// <summary>
/// A package reference.
/// </summary>
/// <param name="Provider">The provider which loads the package.</param>
/// <param name="Configuration">The configuration of the package reference.</param>
public record PackageReference(
    string Provider,
    Dictionary<string, string> Configuration);