using System;
using System.Diagnostics;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Reports how long each step of volume loading and section warping takes, so a benchmark or a
    /// diagnostic tool can time steps that run inside one method without copying that method.
    /// </summary>
    /// <remarks>
    /// <para>Viking itself does not subscribe. With no subscriber, <see cref="Start"/> returns a no-op scope and the cost is one
    /// null check. Subscribers must be thread safe: stages are reported from thread pool threads, and stages for different
    /// sections can be reported concurrently.</para>
    /// <para>Stage names are stable identifiers. Callers that compare runs depend on them, so rename one only together with those callers.</para>
    /// </remarks>
    public static class LoadStageTimings
    {
        /// <summary>Volume.Initialize: download and extract one stos zip. Detail is the stos group name.</summary>
        public const string StosZipFetch = "Volume.StosZipFetch";

        /// <summary>Volume.Initialize: start a load task for every stos in one group. Each load runs synchronously up to its first await, so part of the parsing happens here. Detail is the stos group name.</summary>
        public const string StosQueue = "Volume.StosQueue";

        /// <summary>Volume.Initialize: create a Section for every section element. <c>Section.InitializeFromXML</c> never awaits, so the parsing happens here, on the loading thread.</summary>
        public const string SectionQueue = "Volume.SectionQueue";

        /// <summary>Volume.Initialize: wait for every section element to finish parsing, and register each section.</summary>
        public const string SectionParse = "Volume.SectionParse";

        /// <summary>Volume.Initialize: wait for the stos parse tasks still running after sections finish. Parsing starts as each zip arrives, so most of it overlaps <see cref="StosZipFetch"/> and <see cref="SectionParse"/>.</summary>
        public const string StosParseWait = "Volume.StosParseWait";

        /// <summary>Volume.Initialize: build the registration tree and compose slice-to-volume transforms.</summary>
        public const string CreateVolumeTransforms = "Volume.CreateVolumeTransforms";

        /// <summary>SectionToVolumeMapping: load the section's mosaic tile transforms (parsed cache or .mosaic file). Detail is the section number.</summary>
        public const string MosaicLoad = "Mapping.MosaicLoad";

        /// <summary>SectionToVolumeMapping: read previously warped tiles from the warped-tile cache file. Detail is the section number.</summary>
        public const string WarpCacheRead = "Mapping.WarpCacheRead";

        /// <summary>SectionToVolumeMapping: warp every mosaic tile through the section-to-volume transform. Detail is the section number.</summary>
        public const string Warp = "Mapping.Warp";

        /// <summary>SectionToVolumeMapping: write the warped-tile cache file. Detail is the section number.</summary>
        public const string WarpCacheWrite = "Mapping.WarpCacheWrite";

        /// <summary>
        /// Raised when a stage finishes. Arguments are the stage name (one of the constants on this class), a detail string
        /// such as a section number or group name, and the elapsed time.
        /// </summary>
        public static event Action<string, string, TimeSpan> StageCompleted;

        /// <summary>
        /// Starts timing a stage. Dispose the returned scope when the stage ends; disposing raises <see cref="StageCompleted"/>.
        /// </summary>
        public static StageScope Start(string stage, string detail = null) =>
            StageCompleted is null ? default : new StageScope(stage, detail, Stopwatch.StartNew());

        /// <summary>
        /// Times one stage. A default instance, returned when nobody is listening, does nothing on dispose.
        /// </summary>
        public readonly struct StageScope : IDisposable
        {
            private readonly string _stage;
            private readonly string _detail;
            private readonly Stopwatch _timer;

            internal StageScope(string stage, string detail, Stopwatch timer)
            {
                _stage = stage;
                _detail = detail;
                _timer = timer;
            }

            public void Dispose()
            {
                if (_timer is null)
                    return;

                _timer.Stop();
                StageCompleted?.Invoke(_stage, _detail, _timer.Elapsed);
            }
        }
    }
}
