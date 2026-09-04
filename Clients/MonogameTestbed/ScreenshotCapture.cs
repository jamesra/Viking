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

        public float? LookAtX { get; set; }

        public float? LookAtY { get; set; }

        public double? Downsample { get; set; }

        public string FileSlug
        {
            get
            {
                string stage = ScreenshotCapture.SanitizeFilePart(Stage);
                return Draw3D ? $"{stage}-3d" : $"{stage}-2d";
            }
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
        public static string BajajOutputRoot()
        {
            string basePath = string.IsNullOrWhiteSpace(Program.options?.OutputPath)
                ? Directory.GetCurrentDirectory()
                : Program.options.OutputPath;
            return Path.Combine(basePath, "BajajTest");
        }

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

        public static List<BajajCaptureShot> ResolveRequestedShots(IReadOnlyList<BajajCaptureShot> defaults, IReadOnlyList<CaptureShotRequest> requests)
        {
            if (requests is null || requests.Count == 0)
                return [.. defaults];

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

                resolved.Add(match.WithCamera(request));
            }

            return resolved;
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
