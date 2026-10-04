namespace MortarSmapiBridge;

/// <summary>SMAPI manifests have no maximum version, so the range is enforced here: SMAPI 4 from 4.5, where the reflection
/// lookup was verified. A later 4.x that moved the internal queue leaves the lookup empty and the command channel off.</summary>
internal static class ApiRange
{
    public const int TestedMajor = 4;
    public const int TestedMinor = 5;
    public const string Tested = "4.5 or a later 4.x";
    public const int TestedGameMajor = 1;
    public const int TestedGameMinor = 6;
    public const int TestedGameMinPatch = 14;
    public const string TestedGame = "1.6.14 or a later 1.6.x";

    public static bool IsTested(int major, int minor) => major == TestedMajor && minor >= TestedMinor;

    public static bool IsTestedGame(int major, int minor, int patch) =>
        major == TestedGameMajor && minor == TestedGameMinor && patch >= TestedGameMinPatch;
}
