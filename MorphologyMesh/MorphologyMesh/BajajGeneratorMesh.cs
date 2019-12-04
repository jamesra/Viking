using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Geometry;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MorphologyMesh
{

    /// <summary>
    /// 
    /// Used by the Bajaj generator to represent two sets of polygons, upper and lower, regardless of actual Z levels.
    /// 
    /// MorphRenderMesh class was originally written to handle polygons at arbitrary Z levels.  However, when generating a full mesh it
    /// is possible, if annotators are trying to be difficult, to have annotations with layouts in Z like this.  That have to be grouped 
    /// into a single mesh in order to branch the mesh correctly.
    /// 
    ///  Z = 1:          A
    ///                 / \ 
    ///  Z = 2:        B   \   D
    ///                     \ /
    ///  Z = 3:              C
    ///
    /// In this case the "upper" polygons are A,D and the "lower" polygons are B,C.  Even though B & D are on the same Z level.
    /// </summary>
    public class BajajGeneratorMesh : MorphRenderMesh
    {
        private readonly bool[] IsUpperPolygon;

        public readonly ImmutableSortedSet<int> UpperPolyIndicies;
        public readonly ImmutableSortedSet<int> LowerPolyIndicies;

        private GridPolygon[] UpperPolygons;
        private GridPolygon[] LowerPolygons;

        private List<MorphMeshRegion> _Regions = new List<MorphMeshRegion>();

        public List<MorphMeshRegion> Regions { get; private set; }

        /// <summary>
        /// An optional field that allows tracking of which annotations compose the mesh
        /// </summary>
        public MeshingGroup AnnotationGroup = null;

        public override string ToString()
        {
            string output = "";
            if(AnnotationGroup != null)
            {
                output += AnnotationGroup.ToString() + ":\n\t";
            }

            output += base.ToString();
            return output;
        }

        public BajajGeneratorMesh(IReadOnlyList<GridPolygon> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperPolygon, MeshingGroup group) : this(polygons, ZLevels, IsUpperPolygon)
        {
            AnnotationGroup = group;
        }

        public BajajGeneratorMesh(IReadOnlyList<GridPolygon> polygons, IReadOnlyList<double> ZLevels, IReadOnlyList<bool> IsUpperPolygon) : base(polygons, ZLevels)
        {
            Debug.Assert(polygons.Count == IsUpperPolygon.Count);

            this.IsUpperPolygon = IsUpperPolygon.ToArray();

            //Assign polys to sets for convienience later
            List<int> UpperPolys = new List<int>(IsUpperPolygon.Count);
            List<int> LowerPolys = new List<int>(IsUpperPolygon.Count);
            for (int i = 0; i < IsUpperPolygon.Count; i++)
            {
                if (IsUpperPolygon[i])
                    UpperPolys.Add(i);
                else
                    LowerPolys.Add(i);
            }

            UpperPolyIndicies = UpperPolys.ToImmutableSortedSet<int>();
            LowerPolyIndicies = LowerPolys.ToImmutableSortedSet<int>();

            UpperPolygons = UpperPolys.Select(i => polygons[i]).ToArray();
            LowerPolygons = LowerPolys.Select(i => polygons[i]).ToArray();

        }

        public GridPolygon[] GetSameLevelPolygons(PointIndex key)
        {
            return IsUpperPolygon[key.iPoly] ? UpperPolygons : LowerPolygons;
        }

        public GridPolygon[] GetAdjacentLevelPolygons(PointIndex key)
        {
            return IsUpperPolygon[key.iPoly] ? LowerPolygons : UpperPolygons;
        }

        public GridPolygon[] GetSameLevelPolygons(SliceChord sc)
        {
            return IsUpperPolygon[sc.Origin.iPoly] ? UpperPolygons : LowerPolygons;
        }

        public GridPolygon[] GetAdjacentLevelPolygons(SliceChord sc)
        {
            return IsUpperPolygon[sc.Origin.iPoly] ? LowerPolygons : UpperPolygons;
        }

        public void IdentifyRegionsViaFaces()
        {
            this.Regions = IdentifyRegions(this);
        }

        public MorphMeshRegionGraph IdentifyRegionsViaVerticies(List<MorphMeshVertex> IncompleteVerticies)
        {
            return SecondPassRegionDetection(this, IncompleteVerticies);
        }
    }
}
