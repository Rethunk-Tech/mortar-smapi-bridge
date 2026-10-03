using Xunit;

namespace MortarSmapiBridge.Tests;

public class ApiRangeTests
{
    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(4, 6, true)]
    [InlineData(4, 4, false)]
    [InlineData(5, 5, false)]
    [InlineData(5, 0, false)]
    public void SmapiFourFromTheTestedMinorIsAllowed(int major, int minor, bool expected) =>
        Assert.Equal(expected, ApiRange.IsTested(major, minor));
}
