using Newtonsoft.Json.Linq;

namespace Viking
{
    /// <summary>
    /// Parsed POST /api/viking/launch-exchange body. Snake_case is the documented contract;
    /// camelCase is accepted so a mixed server build still works.
    /// </summary>
    public sealed class VikingLaunchExchangeResult
    {
        public string? AccessToken { get; set; }
        public string? IdentityServerUrl { get; set; }
        public string? VolumeUrl { get; set; }
        public string? VolumeName { get; set; }
        public string? Location { get; set; }
        public string? X { get; set; }
        public string? Y { get; set; }
        public string? Z { get; set; }
        public string? Downsample { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Reads launch-exchange JSON without an HTTP dependency so unit tests can cover both key casings.
    /// Program.cs still owns the POST to /api/viking/launch-exchange.
    /// </summary>
    public static class VikingLaunchExchangeParser
    {
        public static VikingLaunchExchangeResult Failed(string error) => new() { Error = error };

        /// <summary>
        /// Parses a successful exchange body. Returns Failed when JSON is missing or has no access token.
        /// </summary>
        public static VikingLaunchExchangeResult ParseJson(string? responseJson)
        {
            if (string.IsNullOrWhiteSpace(responseJson))
                return Failed("invalid token response");

            JObject? obj;
            try
            {
                obj = JObject.Parse(responseJson);
            }
            catch
            {
                return Failed("invalid token response");
            }

            if (obj is null)
                return Failed("invalid token response");

            string? accessToken = FirstJsonString(obj, "access_token", "accessToken");
            if (string.IsNullOrEmpty(accessToken))
                return Failed("no token returned");

            return new VikingLaunchExchangeResult
            {
                AccessToken = accessToken,
                IdentityServerUrl = FirstJsonString(obj, "identity_server_url", "identityServerUrl"),
                VolumeUrl = FirstJsonString(obj, "volume_url", "volumeUrl"),
                VolumeName = FirstJsonString(obj, "volume_name", "volumeName"),
                Location = FirstJsonString(obj, "location", "location_id", "locationId"),
                X = FirstJsonString(obj, "x"),
                Y = FirstJsonString(obj, "y"),
                Z = FirstJsonString(obj, "z"),
                Downsample = FirstJsonString(obj, "ds", "downsample")
            };
        }

        static string? FirstJsonString(JObject obj, params string[] names)
        {
            foreach (string name in names)
            {
                JToken? token = obj[name];
                if (token is null || token.Type == JTokenType.Null)
                    continue;

                string value = token.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }
}
