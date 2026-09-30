namespace MortarSmapiBridge;

/// <summary>SMAPI manifests have no maximum version, so the range the reflection lookup was verified against is enforced here.</summary>
internal static class ApiRange
{
    public const int TestedMajor = 4;
    public const int TestedMinor = 5;
    public const string Tested = "4.5.x";
    public const int TestedGameMajor = 1;
    public const int TestedGameMinor = 6;
    public const int TestedGamePatch = 15;
    public const string TestedGame = "1.6.15";

    public static bool IsTested(int major, int minor) => major == TestedMajor && minor == TestedMinor;

    public static bool IsTestedGame(int major, int minor, int patch) =>
        major == TestedGameMajor && minor == TestedGameMinor && patch == TestedGamePatch;
}
