using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonogameTestbed
{
    /// <summary>
    /// File-based capture request an agent writes to recapture selected BAJAJTEST views without changing C#.
    /// Loop: run capture, read manifest + PNGs, show the user only unsure images, rewrite this file, re-run.
    /// After a code fix, re-run the same request and compare.
    /// </summary>
    public sealed class CaptureRequestFile
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public int[] Repro { get; set; }

        /// <summary>
        /// Slices named by LocationID rather than by repro index, so a slice found by a diagnostic can be opened in
        /// the viewer without editing the committed repro set.  These are appended after the repro set and selected
        /// automatically when no <see cref="Repro"/> index is given.
        /// </summary>
        public List<ReproLocationRequest> ReproLocations { get; set; }

        public List<CaptureShotRequest> Shots { get; set; }

        /// <summary>
        /// Cameras applied to every 3D shot that does not list its own <see cref="CaptureShotRequest.Cameras"/>.
        /// Each camera produces a separate PNG.  Without this a 3D shot is taken straight down, which is the 2D
        /// view with shading and hides the Z structure of walls, caps, and folds.
        /// </summary>
        public List<CaptureCameraRequest> Cameras3D { get; set; }
    }

    /// <summary>
    /// One 3D camera placement.  Either a <see cref="Preset"/> name, an orbit (<see cref="Azimuth"/>,
    /// <see cref="Elevation"/>, <see cref="Distance"/>) about the slice centre, or an explicit <see cref="Position"/>.
    /// Coordinates are the slice's XY frame; Z is relative to the slice's centre Z, so 0 is mid-slice.
    /// </summary>
    public sealed class CaptureCameraRequest
    {
        /// <summary>Suffix for the PNG name; defaults to the preset name or "azA-elE".</summary>
        public string Name { get; set; }

        /// <summary>One of <see cref="Camera3DPlacement.PresetNames"/>: top, oblique, oblique-back, side, front, below.</summary>
        public string Preset { get; set; }

        /// <summary>Degrees around Z from +X toward +Y, measured from the slice centre to the camera.</summary>
        public double? Azimuth { get; set; }

        /// <summary>Degrees above the XY plane; 90 looks straight down, negative looks up from below.</summary>
        public double? Elevation { get; set; }

        /// <summary>Multiplier on the distance that fits the whole slice in view (default 1).</summary>
        public double? Distance { get; set; }

        /// <summary>Point the camera looks at; defaults to the slice centre.</summary>
        public float[] LookAt { get; set; }

        /// <summary>Explicit camera position; overrides the orbit parameters.</summary>
        public float[] Position { get; set; }

        /// <summary>
        /// Back-face cull as the viewer does (default false).  A slice mesh is an open sheet whose winding faces an
        /// arbitrary side, so with culling on, half the orbit positions render nothing.
        /// </summary>
        public bool? Cull { get; set; }
    }

    /// <summary>
    /// Resolved camera placement: orbit angles or an absolute position, applied by BajajTest against the slice bounds
    /// when the shot is framed.
    /// </summary>
    internal sealed record Camera3DPlacement(string Name, double AzimuthDegrees, double ElevationDegrees, double DistanceScale, float[] LookAt, float[] Position, bool Cull = false)
    {
        public static readonly IReadOnlyDictionary<string, (double Azimuth, double Elevation)> Presets =
            new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase)
            {
                ["top"] = (0, 90),
                ["oblique"] = (240, 35),
                ["oblique-back"] = (60, 35),
                ["side"] = (0, 8),
                ["front"] = (270, 8),
                ["below"] = (240, -35),
            };

        public static IEnumerable<string> PresetNames => Presets.Keys;

        public static Camera3DPlacement FromPreset(string preset)
        {
            if (!Presets.TryGetValue(preset, out (double Azimuth, double Elevation) angles))
                throw new ArgumentException($"Unknown 3D camera preset '{preset}'. Known presets: {string.Join(", ", Presets.Keys)}");

            return new Camera3DPlacement(preset.ToLowerInvariant(), angles.Azimuth, angles.Elevation, 1.0, null, null);
        }

        public static Camera3DPlacement FromRequest(CaptureCameraRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            Camera3DPlacement placement = string.IsNullOrWhiteSpace(request.Preset)
                ? new Camera3DPlacement(null, 240, 35, 1.0, null, null)
                : FromPreset(request.Preset);

            if (request.Azimuth.HasValue)
                placement = placement with { AzimuthDegrees = request.Azimuth.Value };
            if (request.Elevation.HasValue)
                placement = placement with { ElevationDegrees = request.Elevation.Value };
            if (request.Distance.HasValue)
                placement = placement with { DistanceScale = request.Distance.Value };
            if (request.LookAt is { Length: >= 2 })
                placement = placement with { LookAt = request.LookAt };
            if (request.Position is { Length: 3 })
                placement = placement with { Position = request.Position };
            if (request.Cull.HasValue)
                placement = placement with { Cull = request.Cull.Value };

            string name = request.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = placement.Name ?? (placement.Position is not null
                    ? "pos"
                    : $"az{Math.Round(placement.AzimuthDegrees):0}-el{Math.Round(placement.ElevationDegrees):0}");

            return placement with { Name = ScreenshotCapture.SanitizeFilePart(name) };
        }
    }

    /// <summary>
    /// One ad-hoc slice to mesh, identified by the LocationIDs it spans.
    /// </summary>
    public sealed class ReproLocationRequest
    {
        public ulong[] Locations { get; set; }

        /// <summary>Endpoint name such as RC1 or RPC1, or a full OData URL.  Defaults to the -e endpoint.</summary>
        public string Endpoint { get; set; }

        public string Description { get; set; }

        public double? Tolerance { get; set; }
    }

    /// <summary>
    /// One requested screenshot. Omit <see cref="View"/> to accept the default 2D/3D pairing for that stage.
    /// </summary>
    public sealed class CaptureShotRequest
    {
        public string Stage { get; set; }

        public string View { get; set; }

        public float[] LookAt { get; set; }

        public double? Downsample { get; set; }

        /// <summary>3D shots only: one PNG per camera.  Falls back to <see cref="CaptureRequestFile.Cameras3D"/>.</summary>
        public List<CaptureCameraRequest> Cameras { get; set; }
    }

    internal sealed class CaptureManifest
    {
        public List<CaptureManifestCase> Cases { get; set; } = [];
    }

    internal sealed class CaptureManifestCase
    {
        public int Index { get; set; }

        public string Description { get; set; }

        public ulong[] LocationIds { get; set; }

        public string Endpoint { get; set; }

        public string Error { get; set; }

        public string Folder { get; set; }

        /// <summary>Manifold validation of the finished slice; null when generation never reached the final mesh.</summary>
        public string ManifoldReport { get; set; }

        public bool? IsValidSliceSurface { get; set; }

        /// <summary>First published stage whose mesh already had a hole, non-manifold or inconsistent edge.</summary>
        public string FirstInvalidStage { get; set; }

        /// <summary>"stage: report" per published stage, in generation order.</summary>
        public List<string> StageReports { get; set; }

        /// <summary>Individual defect edges of the final mesh (capped).</summary>
        public List<string> Defects { get; set; }

        public List<CaptureManifestShot> Shots { get; set; } = [];
    }

    internal sealed class CaptureManifestShot
    {
        public string Stage { get; set; }

        public string View { get; set; }

        public string RelativePath { get; set; }

        public float LookAtX { get; set; }

        public float LookAtY { get; set; }

        public double Downsample { get; set; }

        /// <summary>3D shots: the camera placement name from the request or preset.</summary>
        public string Camera { get; set; }

        public float[] CameraPosition { get; set; }

        public float[] CameraLookAt { get; set; }
    }

    /// <summary>
    /// One BAJAJTEST overlay combination to draw into a PNG (mesh/line/region stage, 2D vs 3D, optional zoom).
    /// </summary>
    internal sealed class BajajCaptureShot
    {
        public string Stage { get; init; }

        public string View { get; init; } = "2d";

        public int? MeshIndex { get; init; }

        public int? LineIndex { get; init; }

        public int? RegionIndex { get; init; }

        public bool ShowOtvChords { get; init; }

        public bool Draw3D { get; init; }

        public bool ClearVertexLabels { get; init; }

        /// <summary>
        /// Draw the stage's edges without their type labels so the mesh vertex indices are the only text.  Edge labels
        /// and vertex labels compete for the same pixels along a contour, and the vertex numbers are what a defect
        /// report (<c>v12[L:0 iVert:3]</c>) has to be matched against.
        /// </summary>
        public bool VertexIndicesOnly { get; init; }

        public float? LookAtX { get; set; }

        public float? LookAtY { get; set; }

        public double? Downsample { get; set; }

        /// <summary>3D shots only.  Null means the default straight-down framing.</summary>
        public Camera3DPlacement Camera { get; set; }

        public string FileSlug
        {
            get
            {
                string stage = ScreenshotCapture.SanitizeFilePart(Stage);
                if (!Draw3D)
                    return $"{stage}-2d";

                return Camera is null ? $"{stage}-3d" : $"{stage}-3d-{Camera.Name}";
            }
        }

        public BajajCaptureShot WithCamera3D(Camera3DPlacement placement)
        {
            BajajCaptureShot copy = (BajajCaptureShot)MemberwiseClone();
            copy.Camera = placement;
            return copy;
        }

        public static BajajCaptureShot Overview2D() => new()
        {
            Stage = "overview-2d",
            View = "2d",
            ClearVertexLabels = true
        };

        public static BajajCaptureShot OtvChords() => new()
        {
            Stage = "otv-chords",
            View = "2d",
            ShowOtvChords = true,
            ClearVertexLabels = true
        };

        public static BajajCaptureShot Mesh(int index, string name, bool view3d) => new()
        {
            Stage = name,
            View = view3d ? "3d" : "2d",
            MeshIndex = index,
            Draw3D = view3d
        };

        public static BajajCaptureShot Lines(int index, string name) => new()
        {
            Stage = name,
            View = "2d",
            LineIndex = index
        };

        public static BajajCaptureShot Region(int index) => new()
        {
            Stage = $"region-{index}",
            View = "2d",
            RegionIndex = index
        };

        public static BajajCaptureShot VertexIndices(int lineIndex) => new()
        {
            Stage = "vertex-indices",
            View = "2d",
            LineIndex = lineIndex,
            VertexIndicesOnly = true
        };

        public BajajCaptureShot WithCamera(CaptureShotRequest request)
        {
            BajajCaptureShot copy = (BajajCaptureShot)MemberwiseClone();
            if (request.LookAt is { Length: >= 2 })
            {
                copy.LookAtX = request.LookAt[0];
                copy.LookAtY = request.LookAt[1];
            }

            if (request.Downsample.HasValue)
                copy.Downsample = request.Downsample;

            return copy;
        }

        public bool Matches(CaptureShotRequest request)
        {
            if (!ScreenshotCapture.StageKeysEqual(Stage, request.Stage))
                return false;

            if (string.IsNullOrWhiteSpace(request.View))
                return true;

            return string.Equals(View, request.View, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Draws the current frame to a non-MSAA render target and writes PNG. Used by BAJAJTEST capture; reusable later.
    /// </summary>
    internal static class ScreenshotCapture
    {
        /// <summary>
        /// Output folder for a named capture run. BAJAJTEST uses <c>BajajTest</c>; other modes use the TestMode name
        /// so <c>--mode LineStyles --screenshots -o dir</c> lands beside the Bajaj dumps.
        /// </summary>
        public static string OutputRoot(string folderName)
        {
            string basePath = string.IsNullOrWhiteSpace(Program.options?.OutputPath)
                ? Directory.GetCurrentDirectory()
                : Program.options.OutputPath;
            return Path.Combine(basePath, folderName);
        }

        public static string BajajOutputRoot() => OutputRoot("BajajTest");

        public static void SavePng(GraphicsDevice device, string path, Action draw)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(draw);

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int width = Math.Max(1, device.Viewport.Width);
            int height = Math.Max(1, device.Viewport.Height);
            RenderTargetBinding[] previous = device.GetRenderTargets();

            using RenderTarget2D target = new(
                device,
                width,
                height,
                mipMap: false,
                preferredFormat: SurfaceFormat.Color,
                preferredDepthFormat: DepthFormat.Depth24Stencil8,
                preferredMultiSampleCount: 0,
                usage: RenderTargetUsage.PreserveContents);

            device.SetRenderTarget(target);
            try
            {
                device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, MonoTestbed.DefaultBackground, 1f, 0);
                draw();
            }
            finally
            {
                device.SetRenderTargets(previous);
            }

            using FileStream stream = File.Create(path);
            target.SaveAsPng(stream, target.Width, target.Height);
        }

        public static void WriteManifest(string root, CaptureManifest manifest)
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "manifest.json");
            File.WriteAllText(path, JsonSerializer.Serialize(manifest, CaptureRequestFile.JsonOptions));
        }

        public static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().Select(c => (c is ' ' or '/' or '\\' || invalid.Contains(c)) ? '-' : c).ToArray();
            string slug = new string(chars);
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            slug = slug.Trim('-');
            if (slug.Length > 80)
                slug = slug[..80].Trim('-');
            return slug.Length == 0 ? "unnamed" : slug;
        }

        public static bool StageKeysEqual(string left, string right)
        {
            return string.Equals(NormalizeStageKey(left), NormalizeStageKey(right), StringComparison.Ordinal);
        }

        public static List<BajajCaptureShot> ResolveRequestedShots(IReadOnlyList<BajajCaptureShot> defaults, IReadOnlyList<CaptureShotRequest> requests) =>
            ResolveRequestedShots(defaults, requests, defaultCameras: null);

        /// <summary>
        /// Match requests to the default shot list.  A 3D shot is emitted once per camera: its own
        /// <see cref="CaptureShotRequest.Cameras"/>, else <paramref name="defaultCameras"/>, else the single
        /// straight-down framing.  With no requests every default shot is used, and <paramref name="defaultCameras"/>
        /// still fans out the 3D ones.
        /// </summary>
        public static List<BajajCaptureShot> ResolveRequestedShots(IReadOnlyList<BajajCaptureShot> defaults, IReadOnlyList<CaptureShotRequest> requests, IReadOnlyList<CaptureCameraRequest> defaultCameras)
        {
            List<Camera3DPlacement> fallback = ResolveCameras(defaultCameras);

            if (requests is null || requests.Count == 0)
            {
                List<BajajCaptureShot> all = [];
                foreach (BajajCaptureShot shot in defaults)
                    all.AddRange(FanOutCameras(shot, fallback));
                return all;
            }

            List<BajajCaptureShot> resolved = [];
            foreach (CaptureShotRequest request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.Stage))
                    continue;

                BajajCaptureShot match = defaults.FirstOrDefault(d => d.Matches(request));
                if (match is null)
                {
                    TraceWrite($"No BAJAJTEST shot matched stage '{request.Stage}' view '{request.View}'.");
                    continue;
                }

                List<Camera3DPlacement> cameras = request.Cameras is { Count: > 0 } ? ResolveCameras(request.Cameras) : fallback;
                resolved.AddRange(FanOutCameras(match.WithCamera(request), cameras));
            }

            return resolved;
        }

        private static IEnumerable<BajajCaptureShot> FanOutCameras(BajajCaptureShot shot, List<Camera3DPlacement> cameras)
        {
            if (!shot.Draw3D || cameras is null || cameras.Count == 0)
            {
                yield return shot;
                yield break;
            }

            foreach (Camera3DPlacement camera in cameras)
                yield return shot.WithCamera3D(camera);
        }

        private static List<Camera3DPlacement> ResolveCameras(IReadOnlyList<CaptureCameraRequest> requests)
        {
            if (requests is null || requests.Count == 0)
                return null;

            return [.. requests.Select(Camera3DPlacement.FromRequest)];
        }

        /// <summary>
        /// Parse a comma-separated list of preset names (the <c>--cameras</c> option) into camera requests.
        /// </summary>
        public static List<CaptureCameraRequest> ParseCameraPresets(string list)
        {
            if (string.IsNullOrWhiteSpace(list))
                return null;

            List<CaptureCameraRequest> cameras = [];
            foreach (string part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                //Validates the name now so a typo fails at startup rather than after the mesh is built.
                Camera3DPlacement.FromPreset(part);
                cameras.Add(new CaptureCameraRequest { Preset = part });
            }

            return cameras.Count == 0 ? null : cameras;
        }

        private static string NormalizeStageKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string([.. value.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        }

        private static void TraceWrite(string message)
        {
            Console.WriteLine(message);
            System.Diagnostics.Trace.WriteLine(message);
        }
    }
}
