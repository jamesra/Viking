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

        /// <summary>"joint-second-difference-v1" for fields from JointRegistrationSolver; absent for older leave-one-out sets.</summary>
        [JsonPropertyName("method")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Method { get; set; }

        [JsonPropertyName("joint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JointFitDto Joint { get; set; }
    }

    /// <summary>How the joint fit was chosen and how it scored on structures held out of the fit.</summary>
    public sealed class JointFitDto
    {
        [JsonPropertyName("ridge")]
        public double Ridge { get; set; }

        [JsonPropertyName("spatial_weight")]
        public double SpatialWeight { get; set; }

        [JsonPropertyName("pin_section")]
        public int PinSection { get; set; }

        [JsonPropertyName("identity")]
        public bool Identity { get; set; }

        [JsonPropertyName("triples")]
        public int Triples { get; set; }

        [JsonPropertyName("held_out_triples")]
        public int HeldOutTriples { get; set; }

        [JsonPropertyName("held_out_raw_median_nm")]
        public double HeldOutRawMedianNm { get; set; }

        [JsonPropertyName("held_out_corrected_median_nm")]
        public double HeldOutCorrectedMedianNm { get; set; }
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
