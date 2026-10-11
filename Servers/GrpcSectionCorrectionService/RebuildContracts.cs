using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// One annotation context this host corrects. <paramref name="AnnotationDatabase"/> and
    /// <paramref name="StosGroup"/> together are the correction-set key, so contexts that share both are built once.
    /// Rows from the SQL fallback source leave them null, which keys the set by <paramref name="Name"/> as before.
    /// </summary>
    /// <param name="Name">Annotation context name.</param>
    /// <param name="VikingXmlEndpoint">VikingXML URL the stos transforms are loaded from.</param>
    /// <param name="AnnotationEndpoint">Annotation service root of the context's default server.</param>
    /// <param name="AnnotationDatabase">Annotation database name, or null when unknown.</param>
    /// <param name="StosGroup">Context registration name: null for the VikingXML default group.</param>
    /// <param name="ODataEndpoint">OData endpoint of the annotation server, or null.</param>
    public sealed record IdentityVolumeRow(
        string Name,
        string VikingXmlEndpoint,
        string AnnotationEndpoint,
        string AnnotationDatabase = null,
        string StosGroup = null,
        string ODataEndpoint = null)
    {
        /// <summary>Database part of the correction-set key: the annotation database, else the context name.</summary>
        public string CorrectionDatabase => string.IsNullOrWhiteSpace(AnnotationDatabase) ? Name : AnnotationDatabase;
    }

    public interface IIdentityVolumeSource
    {
        Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken);
    }

    public interface IVolumeCorrectionPublisher
    {
        Task<int> PublishVolumeAsync(
            string annotationConnection,
            string volumeUrl,
            Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
            CancellationToken cancellationToken);

        /// <summary>
        /// Publishes reading the annotations through an OData endpoint, for databases with no SQL connection.
        /// </summary>
        Task<int> PublishVolumeFromODataAsync(
            Uri odataEndpoint,
            string volumeUrl,
            Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
            CancellationToken cancellationToken);
    }

    public sealed class VolumeCorrectionPublisherAdapter : IVolumeCorrectionPublisher
    {
        public Task<int> PublishVolumeAsync(
            string annotationConnection,
            string volumeUrl,
            Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
            CancellationToken cancellationToken) =>
            Viking.SectionCorrectionBuilder.CorrectionPublisher.PublishVolumeAsync(
                annotationConnection, volumeUrl, options, cancellationToken);

        public Task<int> PublishVolumeFromODataAsync(
            Uri odataEndpoint,
            string volumeUrl,
            Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
            CancellationToken cancellationToken)
            =>
            Viking.SectionCorrectionBuilder.CorrectionPublisher.PublishVolumeFromODataAsync(
                odataEndpoint, volumeUrl, options, cancellationToken);
    }
}
