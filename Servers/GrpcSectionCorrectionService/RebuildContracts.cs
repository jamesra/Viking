using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.GrpcSectionCorrectionService
{
    public sealed record IdentityVolumeRow(string Name, string VikingXmlEndpoint, string AnnotationEndpoint);

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
    }
}
