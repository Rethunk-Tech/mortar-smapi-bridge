using MortarSmapiBridge.Gmcm;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class GmcmFieldIdsTests
{
    [Fact]
    public void LooksLikeGuid_rejects_32_hex_n_format()
    {
        Assert.False(GmcmFieldIds.LooksLikeGuid("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void LooksLikeGuid_accepts_hyphenated_d_format()
    {
        Assert.True(GmcmFieldIds.LooksLikeGuid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }
}
