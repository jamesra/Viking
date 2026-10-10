using System;

namespace Viking.Common
{
    /// <summary>
    /// Identity accessible-volume fields applied for the current login session.
    /// Set during Viking login; read when resolving the annotation WCF endpoint, export URL, and volume JWT roles.
    /// When unset, clients fall back to VikingXML <c>VolumeToEndpoint</c>.
    /// </summary>
    public static class AccessibleVolumeSession
    {
        /// <summary>
        /// When set, annotation clients prefer this URL over VikingXML <c>VolumeToEndpoint</c>.
        /// </summary>
        public static Uri? AnnotationServiceEndpoint { get; set; }

        /// <summary>
        /// Identity <c>AnnotationServerName</c> used as the OAuth scope resource prefix
        /// (<c>{AnnotationServerName}.review</c>) when it differs from the volume display name.
        /// </summary>
        public static string? AnnotationServerName { get; set; }

        /// <summary>Identity-derived or overridden Export URL for the chosen annotation server.</summary>
        public static Uri? ExportEndpoint { get; set; }

        /// <summary>Identity-derived OData root for the chosen annotation server.</summary>
        public static Uri? ODataEndpoint { get; set; }

        /// <summary>
        /// Clears session overrides (e.g. before a new login attempt).
        /// </summary>
        public static void Clear()
        {
            AnnotationServiceEndpoint = null;
            AnnotationServerName = null;
            ExportEndpoint = null;
            ODataEndpoint = null;
        }

        /// <summary>
        /// Applies Identity <c>AnnotationEndpoint</c> when present; clears the override when absent.
        /// </summary>
        public static void SetAnnotationEndpointFromIdentity(string? annotationEndpointFromIdentity)
        {
            if (IdentityEndpoints.TryResolveAnnotationEndpointUri(annotationEndpointFromIdentity, out Uri endpoint))
                AnnotationServiceEndpoint = endpoint;
            else
                AnnotationServiceEndpoint = null;
        }

        /// <summary>
        /// Stores Identity <c>AnnotationServerName</c> for scope/role checks; clears when blank.
        /// </summary>
        public static void SetAnnotationServerNameFromIdentity(string? annotationServerName)
        {
            AnnotationServerName = string.IsNullOrWhiteSpace(annotationServerName)
                ? null
                : annotationServerName.Trim();
        }

        /// <summary>
        /// Applies optional Export / OData URLs from Identity; clears when blank or invalid.
        /// </summary>
        public static void SetServiceUrlsFromIdentity(string? exportEndpoint, string? odataEndpoint)
        {
            ExportEndpoint = IdentityEndpoints.TryResolveAnnotationEndpointUri(exportEndpoint, out Uri export)
                ? export
                : null;
            ODataEndpoint = IdentityEndpoints.TryResolveAnnotationEndpointUri(odataEndpoint, out Uri odata)
                ? odata
                : null;
        }
    }
}
