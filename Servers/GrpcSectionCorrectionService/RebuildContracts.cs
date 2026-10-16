using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// One annotation context this host corrects. <paramref name="AnnotationDatabase"/> and
    /// <paramref name="StosGroup"/> together are the correction-set key, so contexts that share both are built once.
    /// A context with no annotation database has nothing to correct and is never listed or rebuilt.
    /// </summary>
    /// <param name="Name">Annotation context name.</param>
    /// <param name="VikingXmlEndpoint">VikingXML URL the stos transforms are loaded from.</param>
    /// <param name="AnnotationEndpoint">Annotation service root of the context's default server.</param>
    /// <param name="AnnotationDatabase">Annotation database name from Identity, or null when the context has no annotation server.</param>
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
        /// <summary>Database part of the correction-set key: the annotation database, or null when the context has none.</summary>
        public string CorrectionDatabase => string.IsNullOrWhiteSpace(AnnotationDatabase) ? null : AnnotationDatabase.Trim();

        /// <summary>True when the context has an annotation database, so it can be listed and rebuilt.</summary>
        public bool HasAnnotationDatabase => CorrectionDatabase is not null;
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
