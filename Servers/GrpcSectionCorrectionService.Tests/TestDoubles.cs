using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    sealed class StaticIdentityVolumeSource : IIdentityVolumeSource
    {
        readonly IReadOnlyList<IdentityVolumeRow> _rows;

        public StaticIdentityVolumeSource(IReadOnlyList<IdentityVolumeRow> rows)
        {
            _rows = rows ?? [];
        }

        public Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_rows);
    }

    sealed class RecordingPublisher : IVolumeCorrectionPublisher
    {
        public List<(string Connection, string VolumeUrl, string Output)> Calls { get; } = [];

        public Task<int> PublishVolumeAsync(
            string annotationConnection,
            string volumeUrl,
            CorrectionPublishOptions options,
            CancellationToken cancellationToken)
        {
            Calls.Add((annotationConnection, volumeUrl, options.OutputDirectory));
            if (ThrowOnVolumeUrl is not null && volumeUrl == ThrowOnVolumeUrl)
                throw new InvalidOperationException("publish failed");
            return Task.FromResult(0);
        }

        public string ThrowOnVolumeUrl { get; set; }
    }

    sealed class ThrowingIdentityVolumeSource : IIdentityVolumeSource
    {
        public Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("identity unreachable");
    }
}
