using Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Path = System.IO.Path;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// One published StosGroup directory: manifest, per-Z npz, optional volume.npz.
    /// </summary>
    public sealed class PublishedCorrectionSet
    {
        public const string ManifestFileName = "manifest.json";
        public const string VolumeFileName = "volume.npz";
        public const string PreviewFolderName = "preview";
        const long DenseCellLimit = 4_000_000;

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public PublishedCorrectionSet(
            CorrectionManifestDto manifest,
            IReadOnlyDictionary<long, SectionVectorField> sections)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Sections = sections ?? new Dictionary<long, SectionVectorField>();
        }

        public CorrectionManifestDto Manifest { get; }
        public IReadOnlyDictionary<long, SectionVectorField> Sections { get; }
        public string StosGroup => Manifest.StosGroup;
        public CorrectionProvenanceDto Provenance => Manifest.Provenance;

        public bool TryGetSection(long z, out SectionVectorField field) => Sections.TryGetValue(z, out field);

        public IEnumerable<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> EnumerateLattice()
        {
            foreach (KeyValuePair<long, SectionVectorField> kv in Sections)
            {
                int z = (int)kv.Key;
                foreach (LatticeNode node in kv.Value.Nodes)
                    yield return (z, node.Gx, node.Gy, node.Offset, node.VoteCount);
            }
        }

        public static string SectionFileName(long z) => z.ToString(CultureInfo.InvariantCulture) + ".vfield.npz";

        public static PublishedCorrectionSet LoadDirectory(string directory)
        {
            string manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Correction manifest not found.", manifestPath);

            CorrectionManifestDto manifest = JsonSerializer.Deserialize<CorrectionManifestDto>(
                File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException("Correction manifest was empty.");

            Dictionary<long, SectionVectorField> sections = [];
            IEnumerable<long> zList = manifest.Z ?? [];
            foreach (long z in zList)
            {
                string path = Path.Combine(directory, SectionFileName(z));
                if (!File.Exists(path))
                    continue;
                sections[z] = ReadSectionNpz(path, z, manifest.Provenance);
            }

            return new PublishedCorrectionSet(manifest, sections);
        }

        public void WriteDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
            Manifest.Z = [.. Sections.Keys.OrderBy(z => z)];
            File.WriteAllText(Path.Combine(directory, ManifestFileName), JsonSerializer.Serialize(Manifest, JsonOptions));

            foreach (KeyValuePair<long, SectionVectorField> kv in Sections)
                WriteSectionNpz(Path.Combine(directory, SectionFileName(kv.Key)), kv.Value);

            WriteVolumeNpz(Path.Combine(directory, VolumeFileName));
        }

        public static void WriteSectionNpz(string path, SectionVectorField field)
        {
            (double originX, double originY, int width, int height) = field.OccupiedRasterBounds();
            int gxMin = field.NodeCount == 0 ? 0 : (int)Math.Round(originX / field.PitchNm);
            int gyMin = field.NodeCount == 0 ? 0 : (int)Math.Round(originY / field.PitchNm);

            float[] dx = new float[Math.Max(width * height, 0)];
            float[] dy = new float[dx.Length];
            bool[] valid = new bool[dx.Length];
            int[] votes = new int[dx.Length];
            foreach (LatticeNode node in field.Nodes)
            {
                int ix = node.Gx - gxMin;
                int iy = node.Gy - gyMin;
                if (ix < 0 || iy < 0 || ix >= width || iy >= height)
                    continue;
                int i = iy * width + ix;
                dx[i] = (float)node.Dx;
                dy[i] = (float)node.Dy;
                valid[i] = true;
                votes[i] = node.VoteCount;
            }

            Dictionary<string, NumpyArray> arrays = new()
            {
                ["dx"] = NumpyArray.FromFloat32(dx, height, width),
                ["dy"] = NumpyArray.FromFloat32(dy, height, width),
                ["valid"] = NumpyArray.FromBool(valid, height, width),
                ["n"] = NumpyArray.FromInt32(votes, height, width),
                ["z"] = NumpyArray.ScalarInt64(field.Z),
                ["origin_xy_nm"] = NumpyArray.FromFloat64([originX, originY], 2),
                ["pitch_nm"] = NumpyArray.ScalarFloat64(field.PitchNm),
                ["kernel_radius_nm"] = NumpyArray.ScalarFloat64(field.KernelRadiusNm)
            };
            NumpyNpz.Write(path, arrays);
        }

        public static SectionVectorField ReadSectionNpz(string path, long fallbackZ, CorrectionProvenanceDto provenance)
        {
            Dictionary<string, NumpyArray> arrays = NumpyNpz.Read(path);
            long z = arrays.TryGetValue("z", out NumpyArray zArr) ? zArr.ToInt64()[0] : fallbackZ;
            double pitch = arrays.TryGetValue("pitch_nm", out NumpyArray pitchArr)
                ? pitchArr.ToFloat64()[0]
                : provenance?.PitchNm ?? 2000;
            double kernel = arrays.TryGetValue("kernel_radius_nm", out NumpyArray kernelArr)
                ? kernelArr.ToFloat64()[0]
                : provenance?.KernelRadiusNm ?? 8000;
            double[] origin = arrays.TryGetValue("origin_xy_nm", out NumpyArray originArr)
                ? originArr.ToFloat64()
                : [0, 0];
            float[] dx = arrays["dx"].ToFloat32();
            float[] dy = arrays["dy"].ToFloat32();
            bool[] valid = arrays["valid"].ToBool();
            int[] votes = arrays.TryGetValue("n", out NumpyArray nArr) ? nArr.ToInt32() : null;
            int[] shape = arrays["dx"].Shape;
            int height = shape.Length > 0 ? shape[0] : 0;
            int width = shape.Length > 1 ? shape[1] : dx.Length;
            int gxMin = pitch > 0 ? (int)Math.Round(origin[0] / pitch) : 0;
            int gyMin = pitch > 0 && origin.Length > 1 ? (int)Math.Round(origin[1] / pitch) : 0;

            List<LatticeNode> nodes = [];
            for (int iy = 0; iy < height; iy++)
            {
                for (int ix = 0; ix < width; ix++)
                {
                    int i = iy * width + ix;
                    if (i >= valid.Length || !valid[i])
                        continue;
                    nodes.Add(new LatticeNode(
                        gxMin + ix,
                        gyMin + iy,
                        dx[i],
                        dy[i],
                        votes != null && i < votes.Length ? votes[i] : 3));
                }
            }

            return new SectionVectorField(z, pitch, kernel, nodes);
        }

        void WriteVolumeNpz(string path)
        {
            List<SectionVectorField> fields = [.. Sections.Values.OrderBy(s => s.Z)];
            if (fields.Count == 0)
            {
                NumpyNpz.Write(path, new Dictionary<string, NumpyArray>
                {
                    ["sections"] = NumpyArray.FromInt64([], 0),
                    ["layout"] = NumpyArray.FromInt32([0])
                });
                return;
            }

            double pitch = fields[0].PitchNm;
            int gxMin = int.MaxValue, gxMax = int.MinValue, gyMin = int.MaxValue, gyMax = int.MinValue;
            int totalNodes = 0;
            foreach (SectionVectorField field in fields)
            {
                foreach (LatticeNode node in field.Nodes)
                {
                    gxMin = Math.Min(gxMin, node.Gx);
                    gxMax = Math.Max(gxMax, node.Gx);
                    gyMin = Math.Min(gyMin, node.Gy);
                    gyMax = Math.Max(gyMax, node.Gy);
                    totalNodes++;
                }
            }

            int width = gxMax - gxMin + 1;
            int height = gyMax - gyMin + 1;
            long cells = (long)width * height * fields.Count;
            if (cells > DenseCellLimit)
            {
                WriteVolumeCoo(path, fields, pitch, gxMin, gyMin, totalNodes);
                return;
            }

            float[] dx = new float[cells];
            float[] dy = new float[cells];
            bool[] valid = new bool[cells];
            long[] sections = new long[fields.Count];
            for (int iz = 0; iz < fields.Count; iz++)
            {
                sections[iz] = fields[iz].Z;
                foreach (LatticeNode node in fields[iz].Nodes)
                {
                    int ix = node.Gx - gxMin;
                    int iy = node.Gy - gyMin;
                    long i = ((long)iz * height + iy) * width + ix;
                    dx[i] = (float)node.Dx;
                    dy[i] = (float)node.Dy;
                    valid[i] = true;
                }
            }

            NumpyNpz.Write(path, new Dictionary<string, NumpyArray>
            {
                ["sections"] = NumpyArray.FromInt64(sections, sections.Length),
                ["dx"] = NumpyArray.FromFloat32(dx, fields.Count, height, width),
                ["dy"] = NumpyArray.FromFloat32(dy, fields.Count, height, width),
                ["valid"] = NumpyArray.FromBool(valid, fields.Count, height, width),
                ["origin_xy_nm"] = NumpyArray.FromFloat64([gxMin * pitch, gyMin * pitch], 2),
                ["pitch_nm"] = NumpyArray.ScalarFloat64(pitch),
                ["layout"] = NumpyArray.FromInt32([1])
            });
        }

        static void WriteVolumeCoo(
            string path,
            List<SectionVectorField> fields,
            double pitch,
            int gxMin,
            int gyMin,
            int totalNodes)
        {
            long[] z = new long[totalNodes];
            int[] gx = new int[totalNodes];
            int[] gy = new int[totalNodes];
            float[] dx = new float[totalNodes];
            float[] dy = new float[totalNodes];
            int[] n = new int[totalNodes];
            int i = 0;
            foreach (SectionVectorField field in fields)
            {
                foreach (LatticeNode node in field.Nodes)
                {
                    z[i] = field.Z;
                    gx[i] = node.Gx;
                    gy[i] = node.Gy;
                    dx[i] = (float)node.Dx;
                    dy[i] = (float)node.Dy;
                    n[i] = node.VoteCount;
                    i++;
                }
            }

            NumpyNpz.Write(path, new Dictionary<string, NumpyArray>
            {
                ["z"] = NumpyArray.FromInt64(z, totalNodes),
                ["gx"] = NumpyArray.FromInt32(gx, totalNodes),
                ["gy"] = NumpyArray.FromInt32(gy, totalNodes),
                ["dx"] = NumpyArray.FromFloat32(dx, totalNodes),
                ["dy"] = NumpyArray.FromFloat32(dy, totalNodes),
                ["n"] = NumpyArray.FromInt32(n, totalNodes),
                ["origin_xy_nm"] = NumpyArray.FromFloat64([gxMin * pitch, gyMin * pitch], 2),
                ["pitch_nm"] = NumpyArray.ScalarFloat64(pitch),
                ["layout"] = NumpyArray.FromInt32([0])
            });
        }
    }
}
