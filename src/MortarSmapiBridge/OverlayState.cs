
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
    IReadOnlyDictionary<string, int> Skills,
    string DayOfWeek,
    int DaysLeftInSeason,
    string? FriendName,
    int? FriendHearts,
    string? PetName,
    string? PetType,
    bool? PetWatered);

internal static class OverlayDerived
{
    private const int DaysPerSeason = 28;

    // The game's own rule (WorldDate.GetDayOfWeekFor): day 1 is Monday and day 7 is Sunday.
    internal static string DayOfWeekName(int dayOfMonth) => ((DayOfWeek)(dayOfMonth % 7)).ToString();

    internal static int DaysLeftInSeason(int dayOfMonth) => DaysPerSeason - dayOfMonth;

    /// <summary>The spouse (or roommate) if there is one, else the NPC with the most hearts. Null when nobody has any
    /// hearts, so a fresh save shows no friend instead of an arbitrary name at zero.</summary>
    internal static (string Name, int Hearts)? Friend(string? spouse, IEnumerable<(string Name, int Hearts)> npcs)
    {
        (string Name, int Hearts)[] all = [.. npcs];
        if (!string.IsNullOrEmpty(spouse))
        {
            foreach ((string name, int hearts) in all)
                if (name == spouse)
                    return (name, hearts);
        }

        (string Name, int Hearts)? best = null;
        foreach ((string Name, int Hearts) npc in all.OrderBy(n => n.Name, StringComparer.Ordinal))
            if (npc.Hearts > 0 && (best == null || npc.Hearts > best.Value.Hearts))
                best = npc;
        return best;
    }
}
