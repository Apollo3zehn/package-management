// MIT License
// Copyright (c) [2024] [Apollo3zehn]

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apollo3zehn.PackageManagement.Core;

internal static class JsonSerializerHelper
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string SerializeIndented<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }

    public static void SerializeIndented<T>(Stream utf8Json, T value)
    {
        JsonSerializer.Serialize(utf8Json, value, _options);
    }

    public static Task SerializeIndentedAsync<T>(Stream utf8Json, T value)
    {
        return JsonSerializer.SerializeAsync(utf8Json, value, _options);
    }
}
