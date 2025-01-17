// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using System.Security.Cryptography;
using System.Text;

namespace Apollo3zehn.PackageManagement.Core;

internal static class CustomExtensions
{
    public static byte[] Hash(this string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return hash;
    }
}
