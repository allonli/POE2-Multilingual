using System.Text.Json.Serialization;

namespace Poe2DbLookup.Models;

public sealed record AutocompleteItem(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("desc")] string? Desc,
    [property: JsonPropertyName("class")] string? ClassName);
