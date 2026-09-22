using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;

namespace WebAnnotationTests
{
    [TestClass]
    public class VikingLaunchExchangeParserTests
    {
        [TestMethod]
        public void ParseJson_SnakeCase_ReadsAccessTokenAndVolume()
        {
            const string json = """
                {
                  "access_token": "tok",
                  "identity_server_url": "https://identity.example/",
                  "volume_url": "https://example.com/RC1/",
                  "volume_name": "RC1",
                  "location": "6743"
                }
                """;

            VikingLaunchExchangeResult result = VikingLaunchExchangeParser.ParseJson(json);
            Assert.AreEqual("tok", result.AccessToken);
            Assert.AreEqual("https://identity.example/", result.IdentityServerUrl);
            Assert.AreEqual("https://example.com/RC1/", result.VolumeUrl);
            Assert.AreEqual("RC1", result.VolumeName);
            Assert.AreEqual("6743", result.Location);
            Assert.IsTrue(string.IsNullOrEmpty(result.Error));
        }

        [TestMethod]
        public void ParseJson_CamelCase_StillReadsAccessToken()
        {
            const string json = """{"accessToken":"tok","identityServerUrl":"https://id/","volumeName":"RC2"}""";
            VikingLaunchExchangeResult result = VikingLaunchExchangeParser.ParseJson(json);
            Assert.AreEqual("tok", result.AccessToken);
            Assert.AreEqual("https://id/", result.IdentityServerUrl);
            Assert.AreEqual("RC2", result.VolumeName);
        }

        [TestMethod]
        public void ParseJson_MissingToken_ReturnsError()
        {
            VikingLaunchExchangeResult result = VikingLaunchExchangeParser.ParseJson("""{"volume_name":"RC1"}""");
            Assert.IsTrue(string.IsNullOrEmpty(result.AccessToken));
            Assert.AreEqual("no token returned", result.Error);
        }

        [TestMethod]
        public void ParseJson_InvalidJson_ReturnsError()
        {
            VikingLaunchExchangeResult result = VikingLaunchExchangeParser.ParseJson("not-json");
            Assert.AreEqual("invalid token response", result.Error);
        }
    }
}
