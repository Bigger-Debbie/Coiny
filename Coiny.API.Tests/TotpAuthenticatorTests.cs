using Coiny.API.Services;
namespace Coiny.API.Tests;
public class TotpAuthenticatorTests {
    // RFC 6238 SHA-1 vectors truncated to the standard six-digit authenticator format.
    [Theory]
    [InlineData(59L,"287082")]
    [InlineData(1111111109L,"081804")]
    [InlineData(1111111111L,"050471")]
    [InlineData(1234567890L,"005924")]
    [InlineData(2000000000L,"279037")]
    [InlineData(20000000000L,"353130")]
    public void MatchesRfc6238(long timestamp,string expected) {
        const string key="GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        Assert.Equal(expected,TotpAuthenticator.Code(key,timestamp/30));
        Assert.Equal(timestamp/30,TotpAuthenticator.Verify(key,expected,DateTimeOffset.FromUnixTimeSeconds(timestamp)));
        Assert.Null(TotpAuthenticator.Verify(key,"abcdef",DateTimeOffset.FromUnixTimeSeconds(timestamp)));
    }
}
