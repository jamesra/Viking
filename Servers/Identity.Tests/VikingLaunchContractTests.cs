using System.Security.Claims;
using System.Text.Json;
using Viking.Identity.Server;
using Xunit;

namespace TestIdentityModel
{
    public class VikingLaunchContractTests
    {
        [Fact]
        public void LaunchExchangeResponse_SerializesSnakeCase_NotCamelCase()
        {
            var json = JsonSerializer.Serialize(new LaunchExchangeResponse
            {
                AccessToken = "tok",
                IdentityServerUrl = "https://identity.example/5001",
                VolumeUrl = "http://example/RPC1/SliceToVolume.VikingXML",
                VolumeName = "RPC1"
            });

            Assert.Contains("\"access_token\":\"tok\"", json);
            Assert.Contains("\"identity_server_url\":", json);
            Assert.Contains("\"volume_url\":", json);
            Assert.Contains("\"volume_name\":\"RPC1\"", json);
            Assert.DoesNotContain("accessToken", json);
            Assert.DoesNotContain("identityServerUrl", json);
            Assert.DoesNotContain("volumeUrl", json);
            Assert.DoesNotContain("volumeName", json);
        }

        [Fact]
        public void LaunchCodeRequest_PrefersVolume_Name()
        {
            var request = new LaunchCodeRequest { VolumeName = "RPC1", VolumeNameCamel = "ignored" };
            Assert.Equal("RPC1", request.ResolvedVolumeName);
        }

        [Fact]
        public void LaunchCodeRequest_FallsBackToCamelVolumeName()
        {
            var request = new LaunchCodeRequest { VolumeNameCamel = " RPC1 " };
            Assert.Equal("RPC1", request.ResolvedVolumeName);
        }

        [Fact]
        public void OAuthTokenClient_ReadsClientIdThenAzp()
        {
            var clientId = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("client_id", VikingOAuthClients.SbfsemTools)
            }));
            Assert.True(OAuthTokenClient.IsSbfsemTools(clientId));

            var azp = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("azp", VikingOAuthClients.SbfsemTools)
            }));
            Assert.True(OAuthTokenClient.IsSbfsemTools(azp));

            var viking = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("client_id", "Viking")
            }));
            Assert.False(OAuthTokenClient.IsSbfsemTools(viking));
        }
    }
}
