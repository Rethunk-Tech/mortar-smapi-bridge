using System.Text.Json.Serialization;

namespace MortarSmapiBridge;

internal sealed record OverlaySnapshot(
    [property: JsonPropertyName("location")] string Location,
    [property: JsonPropertyName("playerName")] string PlayerName,
    [property: JsonPropertyName("season")] string Season,
    [property: JsonPropertyName("day")] int Day,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("timeOfDay")] int TimeOfDay,
    [property: JsonPropertyName("money")] int Money,
    [property: JsonPropertyName("weather")] string Weather,
    [property: JsonPropertyName("health")] int Health,
    [property: JsonPropertyName("maxHealth")] int MaxHealth,
    [property: JsonPropertyName("stamina")] float Stamina,
    [property: JsonPropertyName("maxStamina")] int MaxStamina,
    [property: JsonPropertyName("skills")] IReadOnlyDictionary<string, int> Skills);
