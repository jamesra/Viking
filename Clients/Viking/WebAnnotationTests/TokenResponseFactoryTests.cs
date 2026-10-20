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

        [TestMethod]
        public void TryGetBearerAuthorizationValue_UsesAccessTokenWithoutAuthority()
        {
            TokenInjector.BearerTokenAuthority = null;
            TokenInjector.BearerToken = TokenResponseFactory.FromAccessToken("hdr.pay.sig");

            Assert.IsTrue(TokenInjector.TryGetBearerAuthorizationValue(out string header));
            Assert.AreEqual("Bearer hdr.pay.sig", header);
        }

        [TestMethod]
        public void TryGetBearerAuthorizationValue_FalseWhenAccessTokenEmpty()
        {
            TokenInjector.BearerTokenAuthority = "https://identity.example";
            TokenInjector.BearerToken = null;

            Assert.IsFalse(TokenInjector.TryGetBearerAuthorizationValue(out _));
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
