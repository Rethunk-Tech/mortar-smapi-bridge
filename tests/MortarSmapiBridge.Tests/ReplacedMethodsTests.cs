using System.Reflection;
using HarmonyLib;
using MortarSmapiBridge.Startup;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class ReplacedMethodsTests
{
    private static int Skippable() => 1;

    private static int Observed() => 2;

    private static bool SkipPrefix() => true;

    private static void Watch() { }

    private static IEnumerable<CodeInstruction> Same(IEnumerable<CodeInstruction> instructions) => instructions;

    private static MethodInfo Method(string name) => typeof(ReplacedMethodsTests).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    private static Patch By(string owner, string method) => new(Method(method), 0, owner, Priority.Normal, [], [], false);

    // Patching for real needs SMAPI's MonoMod build, which crashes the .NET 8 test host; the registry's records are the input.
    [Fact]
    public void OnlyTranspilersAndBoolPrefixesOfOtherOwnersCount()
    {
        Patches skippable = new([By("Test.Skipper", nameof(SkipPrefix)), By("Test.Watcher", nameof(Watch))], [By("Test.Watcher", nameof(Watch))], [], []);
        Patches observed = new([By("Test.Own.startup", nameof(SkipPrefix))], [], [By("Test.Rewriter", nameof(Same)), By("Test.Own", nameof(Same))], []);

        Dictionary<string, List<string>> replaces = ReplacedMethods.From([(Method(nameof(Skippable)), skippable), (Method(nameof(Observed)), observed), (Method(nameof(Watch)), null)], "Test.Own");

        string type = typeof(ReplacedMethodsTests).FullName!;
        Assert.Equal(["Test.Skipper", "Test.Rewriter"], replaces.Keys);
        Assert.Equal([type + "::Skippable"], replaces["test.skipper"]);
        Assert.Equal([type + "::Observed"], replaces["Test.Rewriter"]);
    }
}
