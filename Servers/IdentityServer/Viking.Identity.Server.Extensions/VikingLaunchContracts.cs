using System.Text.Json.Serialization;

namespace Viking.Identity.Server
{
    /// <summary>POST /api/viking/launch-exchange body. Viking 1.2.61 sends <c>code</c>.</summary>
    public class LaunchExchangeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }
    }

    /// <summary>
    /// POST /api/viking/launch-exchange success body.
    /// Snake_case is the documented wire contract; System.Text.Json would otherwise emit camelCase.
    /// </summary>
    public class LaunchExchangeResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("identity_server_url")]
        public string IdentityServerUrl { get; set; }

        [JsonPropertyName("volume_url")]
        public string VolumeUrl { get; set; }

        [JsonPropertyName("volume_name")]
        public string VolumeName { get; set; }
    }

    /// <summary>
    /// POST /api/viking/launch-code body. Canonical key is <c>volume_name</c>;
    /// <c>volumeName</c> is accepted so a mixed client build still works.
    /// </summary>
    public class LaunchCodeRequest
    {
        [JsonPropertyName("volume_name")]
        public string VolumeName { get; set; }

        [JsonPropertyName("volumeName")]
        public string VolumeNameCamel { get; set; }

        /// <summary>Identity volume name from either JSON key.</summary>
        [JsonIgnore]
        public string ResolvedVolumeName =>
            !string.IsNullOrWhiteSpace(VolumeName) ? VolumeName.Trim()
            : VolumeNameCamel?.Trim();
    }

    /// <summary>POST /api/viking/launch-code success body. Location is not stored; the caller appends it to <see cref="VikingUrl"/>.</summary>
    public class LaunchCodeResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("viking_url")]
        public string VikingUrl { get; set; }
    }
}
