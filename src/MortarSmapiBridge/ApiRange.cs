namespace MortarSmapiBridge;

/// <summary>SMAPI manifests have no maximum version, so the range the reflection lookup was verified against is enforced here.</summary>
internal static class ApiRange
{
    public const int TestedMajor = 4;
    public const int TestedMinor = 5;
    public const string Tested = "4.5.x";

    public static bool IsTested(int major, int minor) => major == TestedMajor && minor == TestedMinor;
}
