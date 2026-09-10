using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Helpdesk.Infrastructure.Persistence;

public static class EfJsonConverters
{
    public static ValueConverter<List<T>, string> JsonList<T>() where T : class, new()
    {
        return new ValueConverter<List<T>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<T>()
                : JsonSerializer.Deserialize<List<T>>(v, (JsonSerializerOptions?)null) ?? new());
    }

    public static ValueConverter<float[], string> FloatArrayToJson { get; } =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>());

    public static ValueConverter<List<string>, string> StringListToJson { get; } =
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());

    public static ValueConverter<List<T>, string> EnumListToJson<T>() where T : struct, Enum =>
        new(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v) ? new List<T>() : JsonSerializer.Deserialize<List<T>>(v, (JsonSerializerOptions?)null) ?? new());

    public static ValueConverter<JsonDocument, string> JsonDocumentToString { get; } =
        new(
            v => v.RootElement.GetRawText(),
            v => ParseJsonDocument(v));

    public static ValueConverter<Vector, string> VectorToJson { get; } =
        new(v => JsonSerializer.Serialize(v.ToArray(), (JsonSerializerOptions?)null),
            v => new Vector(JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>()));

    private static JsonDocument ParseJsonDocument(string? value)
    {
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "{}" : value!, new JsonDocumentOptions());
    }
}
