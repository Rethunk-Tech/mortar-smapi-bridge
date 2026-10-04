
namespace MortarSmapiBridge;

internal sealed record OverlaySnapshot(
    bool InGame,
    string Location,
    string PlayerName,
    string Season,
    int Day,
    int Year,
    int TimeOfDay,
    int Money,
    string Weather,
    int Health,
    int MaxHealth,
    float Stamina,
    int MaxStamina,
    IReadOnlyDictionary<string, int> Skills);
