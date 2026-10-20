using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.Text.Json;
using Viking.Tokens;

namespace WebAnnotationTests
{
    [TestClass]
    public class TokenResponseFactoryTests
    {
        [TestMethod]
        public void FromAccessToken_SetsAccessTokenReadableByDuendeGetter()
        {
            const string jwt = "header.payload.signature";
            var response = TokenResponseFactory.FromAccessToken(jwt);
            Assert.IsNotNull(response);
            Assert.AreEqual(jwt, response.AccessToken);
        }

        [TestMethod]
        public void FromAccessToken_Empty_ReturnsNull()
        {
            Assert.IsNull(TokenResponseFactory.FromAccessToken(""));
            Assert.IsNull(TokenResponseFactory.FromAccessToken("   "));
            Assert.IsNull(TokenResponseFactory.FromAccessToken(null));
        }

        /// <summary>
        /// The gRPC clients read <see cref="TokenStore.BearerToken"/>'s AccessToken for the Authorization header, so a
        /// token built from a launch-code JWT must expose that JWT (Duende parses it from the response JSON).
        /// </summary>
        [TestMethod]
        public void FromAccessToken_ExposesTheJwtAsAccessToken()
        {
            TokenStore.BearerTokenAuthority = null;
            TokenStore.BearerToken = TokenResponseFactory.FromAccessToken("hdr.pay.sig");

            Assert.AreEqual("hdr.pay.sig", TokenStore.BearerToken.AccessToken);
        }

        [TestMethod]
        public void ContainsVolumeRead_AcceptsHyphenatedAndRawScopes()
        {
            string jwt = FakeJwtWithScope("RC2.Read openid");
            Assert.IsTrue(JwtAccessTokenScopes.ContainsVolumeRead(jwt, "RC2"));

            string spaced = FakeJwtWithScope("My-Volume.Read");
            Assert.IsTrue(JwtAccessTokenScopes.ContainsVolumeRead(spaced, "My Volume"));
        }

        static string FakeJwtWithScope(string scope)
        {
            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"));
            string payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { scope })));
            return $"{header}.{payload}.sig";
        }

        static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
