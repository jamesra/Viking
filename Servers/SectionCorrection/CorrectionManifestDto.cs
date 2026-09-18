using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Viking.SectionCorrection
{
    public sealed class CorrectionManifestDto
    {
        [JsonPropertyName("stos_group")]
        public string StosGroup { get; set; }

        [JsonPropertyName("provenance")]
        public CorrectionProvenanceDto Provenance { get; set; }

        [JsonPropertyName("volume_url")]
        public string VolumeUrl { get; set; }

        [JsonPropertyName("z")]
        public List<long> Z { get; set; }
    }

    public sealed class CorrectionProvenanceDto
    {
        [JsonPropertyName("built_utc")]
        public DateTime BuiltUtc { get; set; }

        [JsonPropertyName("annotation_watermark")]
        public DateTime AnnotationWatermark { get; set; }

        [JsonPropertyName("pitch_nm")]
        public double PitchNm { get; set; }

        [JsonPropertyName("kernel_radius_nm")]
        public double KernelRadiusNm { get; set; }

        [JsonPropertyName("min_annotation_votes")]
        public int MinAnnotationVotes { get; set; }

        [JsonPropertyName("residual_window")]
        public ResidualWindowDto ResidualWindow { get; set; }
    }

    public sealed class ResidualWindowDto
    {
        [JsonPropertyName("min_both")]
        public int MinBoth { get; set; }

        [JsonPropertyName("max_both")]
        public int MaxBoth { get; set; }

        [JsonPropertyName("min_one")]
        public int MinOne { get; set; }

        [JsonPropertyName("max_one")]
        public int MaxOne { get; set; }
    }
}
