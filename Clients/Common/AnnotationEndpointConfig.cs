#nullable enable annotations
using System.Linq;
using System.Xml.Linq;

namespace Viking.Common
{
    /// <summary>
    /// Whether the open volume has an annotation database: the rule the WebAnnotation extension loads by, shared so the
    /// viewer can tell a plain image stack from an annotated volume without referencing the extension.
    /// </summary>
    public static class AnnotationEndpointConfig
    {
        /// <summary>
        /// True when the login chose an annotation server (<see cref="AccessibleVolumeSession.AnnotationServiceEndpoint"/>),
        /// or when <paramref name="volumeElement"/> (the VikingXML <c>Volume</c> element) has a <c>VolumeToEndpoint</c> child,
        /// the first of which has a non-blank <c>Endpoint</c> attribute. False for a null element otherwise.
        /// </summary>
        public static bool IsConfigured(XElement? volumeElement)
        {
            if (AccessibleVolumeSession.AnnotationServiceEndpoint != null)
                return true;

            XElement? mapping = volumeElement?.Elements().FirstOrDefault(e => e.Name.LocalName == "VolumeToEndpoint");
            XAttribute? endpoint = mapping?.Attribute("Endpoint");
            return endpoint is not null && !string.IsNullOrWhiteSpace(endpoint.Value);
        }
    }
}
