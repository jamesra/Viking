using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.Tokens;

namespace WebAnnotationTests
{
    [TestClass]
    public class AccessibleVolumesPayloadTests
    {
        /// <summary>
        /// Identity sends Mirrors as objects. A mismatched element type once made the whole response fail to parse,
        /// which login swallowed, so tokens were scoped by context name (RC1-Pitt.Read) and the annotation service denied them.
        /// </summary>
        [TestMethod]
        public void PayloadWithMirrorObjectsDeserializesAndResolvesAnnotationServer()
        {
            const string json = @"{""7"":{""id"":7,""name"":""RC1-Pitt"",""annotationServerName"":""RC1"",
                ""mirrors"":[{""url"":""https://example.com/RC1.VikingXML"",""region"":""US"",""priority"":0}],
                ""annotationServers"":[{""name"":""RC1"",""isDefault"":true}],
                ""volume"":{""id"":3,""name"":""RC1""},""permissions"":[""Read""]}}";
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var volumes = JsonSerializer.Deserialize<Dictionary<long, UserResourcePermissions>>(json, options);

            Assert.AreEqual("RC1", AccessibleVolumeCatalog.FindMatch(volumes.Values, "RC1-Pitt").AnnotationServerName);
            Assert.AreEqual("US", volumes[7].Mirrors[0].Region);
        }
    }
}
