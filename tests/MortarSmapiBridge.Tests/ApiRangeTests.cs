using Xunit;

namespace MortarSmapiBridge.Tests;

public class ApiRangeTests
{
    [Theory]
    [InlineData(4, 5, 2, true)]
    [InlineData(4, 5, 1, false)]
    [InlineData(4, 5, 0, false)]
    [InlineData(4, 6, 0, true)]
    [InlineData(4, 4, 9, false)]
    [InlineData(5, 5, 0, false)]
    [InlineData(5, 0, 0, false)]
    public void SmapiFourFromTheTestedPatchIsAllowed(int major, int minor, int patch, bool expected) =>
        Assert.Equal(expected, ApiRange.IsTested(major, minor, patch));
}
