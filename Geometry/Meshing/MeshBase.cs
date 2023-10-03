﻿
//#define TRACEMESH

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace Geometry.Meshing
{
    public interface IMesh3D<VERTEX> : IReadOnlyMesh3D<VERTEX>, IMesh<VERTEX>
        where VERTEX : IVertex3D
    {
        //new IReadOnlyList<VERTEX> Verticies { get; }

    }

    public interface IReadOnlyMesh3D<out VERTEX> : IReadOnlyMesh<VERTEX>
    where VERTEX : IVertex3D
    {
        GridBox BoundingBox { get; }
    }


    public interface IMesh2D<VERTEX> : IReadOnlyMesh2D<VERTEX>, IMesh<VERTEX>
        where VERTEX : IVertex2D
    {

    }

    public interface IReadOnlyMesh2D<out VERTEX> : IReadOnlyMesh<VERTEX>
    where VERTEX : IVertex2D
    {
        //new IReadOnlyList<VERTEX> Verticies { get; }

        GridLineSegment ToGridLineSegment(IEdgeKey key);

        GridLineSegment ToGridLineSegment(long A, long B);

        /// <summary>
        /// Return a normalized vector with origin at A towards B
        /// </summary> 
        /// <returns></returns>
        GridLine ToGridLine(IEdgeKey key);

        /// <summary>
        /// Return a normalized vector from the Origin towards the Direction vertex
        /// </summary>
        /// <param name="Origin"></param>
        /// <param name="Direction"></param>
        /// <returns></returns>
        GridLine ToGridLine(long Origin, long Direction);

        bool IsClockwise(IFace f);

        RotationDirection Winding(IFace f);

    }

    public interface IReadOnlyMesh<out VERTEX>
        where VERTEX : IVertex
    {
        IReadOnlyList<VERTEX> Verticies { get; }
        Dictionary<IEdgeKey, IEdge> Edges { get; } //If you are ever tempted to try a sortedlist profiling showed dictionary to be much faster during bajaj mesh generation
        SortedSet<IFace> Faces { get; }

        VERTEX this[long index] { get; }
        VERTEX this[int index] { get; }

        IEnumerable<VERTEX> this[IEnumerable<int> vertIndicies] { get; }
        IEnumerable<VERTEX> this[IEnumerable<long> vertIndicies] { get; }

        IEdge this[IEdgeKey key] { get; }

        bool Contains(IEdgeKey key);
        bool Contains(IFace key);

        bool Contains(int A, int B);
    }

    public interface IMesh<VERTEX> : IReadOnlyMesh<VERTEX>
        where VERTEX : IVertex
    {
        /// <summary>
        /// Add vertex to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Index of vertex</returns>
        int AddVertex(VERTEX v);

        int AddVerticies(IEnumerable<VERTEX> verts);

        void AddEdge(int A, int B);

        void AddEdge(IEdgeKey e);

        void AddEdge(IEdge e);

        void RemoveEdge(IEdgeKey e);

        void AddFace(IFace face);

        void AddFace(int A, int B, int C);

        void AddFaces(ICollection<IFace> faces);

        void RemoveFace(IFace f);

        void SplitFace(IFace face);
    }

    /*
    /// <summary>
    /// Descriptions for mesh change events
    /// </summary>
    public enum MeshChange
    {
        AddVertex,
        AddEdge,
        AddFace,
        RemoveVertex,
        RemoveEdge,
        RemoveFace
    }

    public struct MeshChangeEventArgs
    {
        MeshChange Action;

        IVertex[] added_verts;
        IEdgeKey[] added_edges;
        IFace[] added_faces;

        IVertex[] removed_verts;
        IEdgeKey[] removed_edges;
        IFace[] removed_faces;
         
        public MeshChangeEventArgs(MeshChange action)
        {
            Action = action;
            added_verts = new IVertex2D[0];
            added_edges = new IEdgeKey[0];
            added_faces = new IFace[0];

            removed_verts = new IVertex[0];
            removed_edges = new IEdgeKey[0];
            removed_faces = new IFace[0];
        }

        public static MeshChangeEventArgs Add(IVertex[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.AddVertex);
            obj.added_verts = added;
            return obj;
        }

        public static MeshChangeEventArgs Add(IEdgeKey[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.AddEdge);
            obj.added_edges = added;
            return obj;
        }

        public static MeshChangeEventArgs Add(IFace[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.AddFace);
            obj.Action = MeshChange.AddFace;
            obj.added_faces = added;
            return obj;
        }

        public static MeshChangeEventArgs Remove(IVertex[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.RemoveVertex);
            obj.removed_verts = added;
            return obj;
        }

        public static MeshChangeEventArgs Remove(IEdgeKey[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.RemoveEdge);
            obj.removed_edges = added;
            return obj;
        }

        public static MeshChangeEventArgs Remove(IFace[] added)
        {
            MeshChangeEventArgs obj = new MeshChangeEventArgs(MeshChange.RemoveFace);
            obj.removed_faces = added;
            return obj;
        }
    }
    */


    /// <summary>
    /// A class that implements the basic mesh operations
    /// </summary>
    /// <typeparam name="VERTEX"></typeparam>
    public abstract class MeshBase<VERTEX> : IMesh<VERTEX>
        where VERTEX : IVertex
    {
        protected readonly List<VERTEX> _Verticies = new List<VERTEX>();
        protected readonly Dictionary<IEdgeKey, IEdge> _Edges = new Dictionary<IEdgeKey, IEdge>();
        protected readonly SortedSet<IFace> _Faces = new SortedSet<IFace>();

        //        public event MeshChangeEvent OnMeshChange;
        //        public delegate void MeshChangeEvent(MeshBase<VERTEX> mesh, MeshChangeEventArgs e);

        public virtual IReadOnlyList<VERTEX> Verticies => _Verticies;
        public Dictionary<IEdgeKey, IEdge> Edges => _Edges;
        public SortedSet<IFace> Faces => _Faces;

        /* Functions for mesh users to override how mesh objects are created*/
        public Func<VERTEX, int, VERTEX> CreateOffsetVertex { get; set; }
        public Func<IEdge, int, int, IEdge> CreateOffsetEdge { get; set; }
        public Func<IFace, IEnumerable<int>, IFace> CreateOffsetFace { get; set; }
        public Func<int, VERTEX> CreateVertex { get; set; }
        public Func<int, int, IEdge> CreateEdge { get; set; }
        public Func<IEnumerable<int>, IFace> CreateFace { get; set; }

        public virtual VERTEX this[int key]
        {
            get => _Verticies[key];
            set => _Verticies[key] = value;
        }

        public virtual VERTEX this[long key]
        {
            get => _Verticies[(int)key];
            set => _Verticies[(int)key] = value;
        }

        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<VERTEX> this[IEnumerable<int> vertIndicies]
        {
            get => vertIndicies.Select(i => this._Verticies[(int)i]);
        }

        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<VERTEX> this[IEnumerable<long> vertIndicies]
        {
            get => vertIndicies.Select(i => this._Verticies[(int)i]);
        }

        /// <summary>
        /// Returns all of the verticies in the face
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<VERTEX> this[IFace face]
        {
            get => face.iVerts.Select(i => this._Verticies[(int)i]);
        }

        public virtual IEdge this[IEdgeKey key] => this._Edges[key];

        /// <summary>
        /// Returns all of the verticies that match the indicies
        /// </summary>
        /// <param name="vertIndicies"></param>
        /// <returns></returns>
        public IEnumerable<IEdge> this[IEnumerable<IEdgeKey> keys]
        {
            get => keys.Select(e => this._Edges[e]);
        }

        public virtual bool Contains(IEdgeKey key) => Edges.ContainsKey(key);

        public virtual bool Contains(IFace face) => Faces.Contains(face);

        public virtual bool Contains(int A, int B) => Edges.ContainsKey(new EdgeKey(A, B));

        public virtual bool Contains(long A, long B) => Edges.ContainsKey(new EdgeKey((int)A, (int)B));

        /// <summary>
        /// Adds the vertex.  If a vertex already has an index that does not match the next index an ArgumentException is thrown
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public virtual int AddVertex(VERTEX v)
        { 
            if(v.HasIndex && v.Index != _Verticies.Count)
                throw new ArgumentException("Vertex has an index that doesn't match the index we want to assign");

            v.SetIndex(_Verticies.Count);

            _Verticies.Add(v);

            UpdateBoundingBox(v);
            return v.Index;
        }

        /// <summary>
        /// Add a collection of verticies to the mesh whose index has not been set
        /// </summary>
        /// <param name="v"></param>
        /// <returns>The index the first element was inserted at</returns>
        public virtual int AddVerticies(IEnumerable<VERTEX> verts)
        {
            int iStart = _Verticies.Count;
            int Offset = 0;
            foreach (var v in verts)
            {
                //In some cases callers to AddVerticies need tight control over the index of the vertex
                //If the vertex has an index, check that it matches the index we want to assign. If it 
                //doesn't more work needs to be done to handle this case
                if(v.HasIndex && v.Index != iStart + Offset)
                {
                    throw new ArgumentException("Vertex has an index that doesn't match the index we want to assign");
                }

                v.SetIndex(iStart + Offset);
                Offset += 1;
            }

            //If verts was empty just exit;
            if (Offset == 0)
                return iStart;

            _Verticies.AddRange(verts);
            UpdateBoundingBox(verts);
            return iStart;
        }


        protected abstract void UpdateBoundingBox(VERTEX point);
        protected abstract void UpdateBoundingBox(IEnumerable<VERTEX> points);

        public void AddEdge(int A, int B)
        {
            EdgeKey e = new EdgeKey(A, B);
            AddEdge(e);
        }

        public void AddEdge(IEdgeKey e)
        {
            if (e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

            if (this.Contains(e))
                return;

            if (CreateEdge == null)
                throw new InvalidOperationException(string.Format("Adding {0}: DuplicateEdge function not specified for DynamicRenderMesh", e));
            /*
            if (e.A >= _Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= _Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));
                */
#if TRACEMESH
            Trace.WriteLine(string.Format("Add edge {0}", e));
#endif

            IEdge newEdge = CreateEdge(e.A, e.B);

            this.AddEdge(newEdge);

            /*
            Edges.Add(e, newEdge);

            _Verticies[(int)e.A].AddEdge(e);
            _Verticies[(int)e.B].AddEdge(e);
            */
        }


        public virtual void AddEdge(IEdge e)
        {
            if (e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

            if (this.Contains(e.Key))
                return;

            if (e.A >= _Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= _Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

#if TRACEMESH
            Trace.WriteLine(string.Format("Add edge {0}", e));
#endif

            Edges.Add(e.Key, e);

            _Verticies[(int)e.A].AddEdge(e.Key);
            _Verticies[(int)e.B].AddEdge(e.Key);
        }

        public virtual void RemoveEdge(IEdgeKey e)
        {
#if TRACEMESH
            Trace.WriteLine(string.Format("Remove edge {0}", e));
#endif
            if (_Edges.TryGetValue(e, out IEdge removedEdge))
            {
                foreach (IFace f in removedEdge.Faces)
                {
                    this.RemoveFace(f);
                }

                _Edges.Remove(e);

                this[removedEdge.A].RemoveEdge(e);
                this[removedEdge.B].RemoveEdge(e);
            }
        }

        /// <summary>
        /// Add a face. Creates edges if they aren't in the face
        /// </summary>
        /// <param name="face"></param>
        public virtual void AddFace(IFace face)
        {
            //Debug.Assert(Faces.Contains(face) == false, string.Format("Mesh already contains {0}", face));
#if TRACEMESH
            Trace.WriteLine(string.Format("Add face {0}", face));
#endif

            AddFaceToEdges(face);

            Faces.Add(face);
        }

        public void AddFace(int A, int B, int C)
        {
            IFace face = CreateFace(new int[] { A, B, C });
            Debug.Assert(Faces.Contains(face) == false);

            AddFace(face);
        }

        /// <summary>
        /// Adds a face to edges.  This is a virtual method so that 2D meshes can throw an error if an edge has more than two faces
        /// </summary>
        /// <param name="face"></param>
        protected virtual void AddFaceToEdges(IFace face)
        {
            foreach (IEdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].AddFace(face);
            }
        }

        public void AddFaces(ICollection<IFace> faces)
        {
            foreach (IFace f in faces)
            {
                AddFaceToEdges(f);
            }

            Faces.UnionWith(faces);
        }

        public void RemoveFace(IFace f)
        {
            if (Faces.Contains(f))
            {

#if TRACEMESH
                Trace.WriteLine(string.Format("Remove face {0}", f));
#endif

                Faces.Remove(f);

                foreach (IEdgeKey e in f.Edges)
                {
                    IEdge existing = Edges[e];
                    existing.RemoveFace(f);
                }
            }
        }

        #region Path finding along faces

        /// <summary>
        /// Returns the shortest path of adjacent faces from the starting face to a face meeting a criteria function
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="start">Starting Face</param>
        /// <param name="CanBePartOfPath">Returns true if the tested face can be part of the path.</param>
        /// <param name="MeetsCriteriaFunc">Returns true if the face is the desired destination</param>
        /// <returns></returns>
        public List<IFace> FindFacesInPath(IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc)
        {
            SortedSet<IFace> testedFaces = new SortedSet<IFace>();
            Dictionary<IFace, List<IFace>> PathCache = new Dictionary<IFace, List<IFace>>();
            return RecurseFacePath(ref testedFaces, this, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        public List<IFace> FindFacesInPath(IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
        {
            Dictionary<IFace, List<IFace>> PathCache = new Dictionary<IFace, List<IFace>>();
            return RecurseFacePath(ref CheckedFaces, this, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        /// <summary>
        /// Recursively search for the shortest path between two faces by walking adjacent faces whose shared edges meet a criteria function and whose faces meet a criteria function
        /// </summary>
        /// <param name="testedFaces"></param>
        /// <param name="mesh"></param>
        /// <param name="Origin"></param>
        /// <param name="IsMatch"></param>
        /// <param name="PathCache">Contains a lookup table of the shortest route to the target for each face</param>
        /// <returns></returns>
        private static List<IFace> RecurseFacePath(ref SortedSet<IFace> testedFaces, MeshBase<VERTEX> mesh, IFace Origin, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> IsMatch, Dictionary<IFace, List<IFace>> PathCache)
        {
            //System.Diagnostics.Trace.WriteLine(Origin.ToString());
            testedFaces.Add(Origin);

            List<IFace> path = new List<IFace>
            {
                Origin
            };

            if (IsMatch(Origin))
                return path;

            if(PathCache.TryGetValue(Origin, out var pathCacheResult))
            {
                return pathCacheResult;
            }

            SortedSet<IFace> untestedFaces = new SortedSet<IFace>(mesh.AdjacentFaces(Origin));
            untestedFaces.ExceptWith(testedFaces);

            if (untestedFaces.Count == 0)
                return null;
            else if (untestedFaces.Count == 1)
            {

                IFace adjacentFace = untestedFaces.First();

                //Check if the face can be part of the path, if not don't bother investigating this route
                if (!CanBePartOfPath(adjacentFace))
                {
                    testedFaces.Add(adjacentFace);
                    return null;
                }

                List<IFace> result = RecurseFacePath(ref testedFaces, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
                if (result == null)
                    return null;

                path.AddRange(result);
                PathCache[Origin] = path;
                return path;
            }
            else
            {
                List<List<IFace>> listPotentialPaths = new List<List<IFace>>(untestedFaces.Count);
                SortedSet<IFace> AllBranchesTested = new SortedSet<IFace>();
                foreach (IFace adjacentFace in untestedFaces)
                {
                    if (testedFaces.Contains(adjacentFace))
                        continue;

                    //Check if the face can be part of the path, if not don't bother investigating this route
                    if (!CanBePartOfPath(adjacentFace))
                    {
                        testedFaces.Add(adjacentFace);
                        continue;
                    }

                    SortedSet<IFace> testedFacesCopy = new SortedSet<IFace>(testedFaces);
                    List<IFace> result = RecurseFacePath(ref testedFacesCopy, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
                    if (result == null)
                    {
                        //We know none of the faces lead to the target so don't bother checking them again
                        testedFaces.UnionWith(testedFacesCopy);
                        continue;
                    }

                    AllBranchesTested.UnionWith(testedFacesCopy);
                    listPotentialPaths.Add(result);
                }

                //Add the faces we tested so we don't check again
                testedFaces.UnionWith(AllBranchesTested);

                //If no paths lead to destination, return null. 
                if (listPotentialPaths.Count == 0)
                    return null;

                //Otherwise, select the shortest path
                int MinDistance = listPotentialPaths.Select(L => L.Count).Min();
                List<IFace> shortestPath = listPotentialPaths.First(L => L.Count == MinDistance);
                path.AddRange(shortestPath);
                PathCache[Origin] = path;
                return path;
            }
        }

        /// <summary>
        /// Returns a list of faces adjacent to the passed face
        /// </summary>
        /// <param name="face"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public IFace[] AdjacentFaces(IFace face)
        {
            return face.Edges.SelectMany(e => this[e].Faces.Where(f => f.Equals(face) == false)).ToArray();
        }

        public abstract void SplitFace(IFace face);



        #endregion

        #region Edge path 

        /*
        /// <summary>
        /// Returns the shortest path of adjacent faces from the starting face to a face meeting a criteria function
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="start">Starting Face</param>
        /// <param name="CanBePartOfPath">Returns true if the tested face can be part of the path.</param>
        /// <param name="MeetsCriteriaFunc">Returns true if the face is the desired destination</param>
        /// <returns></returns>
        public List<IEdge> FindEdgesInPath(IEdgeKey start, Func<List<IEdge>, IEdge, bool> CanBePartOfPath, Func<List<IEdge>, IEdge, bool> MeetsCriteriaFunc)
        {
            SortedSet<IEdgeKey> testedEdges = new SortedSet<IEdgeKey>();
            Dictionary<IEdge, List<IEdge>> PathCache = new Dictionary<IEdge, List<IEdge>>();
            return RecurseEdgePath(ref testedEdges, this, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        public List<IEdge> FindEdgesInPath(IEdgeKey start, Func<List<IEdge>, IEdge, bool> CanBePartOfPath, Func<List<IEdge>, IEdge, bool> MeetsCriteriaFunc, ref SortedSet<IEdgeKey> testedEdges)
        {
            Dictionary<IEdge, List<IEdge>> PathCache = new Dictionary<IEdge, List<IEdge>>();
            return RecurseEdgePath(ref testedEdges, this, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }


        /// <summary>
        /// Recursively search for the shortest path between two faces by walking adjacent faces whose shared edges meet a criteria function and whose faces meet a criteria function
        /// </summary>
        /// <param name="testedEdges"></param>
        /// <param name="mesh"></param>
        /// <param name="Origin"></param>
        /// <param name="IsMatch"></param>
        /// <param name="PathCache">Contains a lookup table of the shortest route to the target for each face</param>
        /// <returns></returns>
        private static List<IEdge> RecurseEdgePath(ref SortedSet<IEdgeKey> testedEdges, MeshBase<VERTEX> mesh, IEdgeKey OriginKey, Func<List<IEdge>, IEdge, bool> CanBePartOfPath, Func<List<IEdge>, IEdge, bool> IsMatch, Dictionary<IEdge, List<IEdge>> PathCache)
        {
            IEdge Origin = mesh[OriginKey];
            //System.Diagnostics.Trace.WriteLine(Origin.ToString());
            testedEdges.Add(Origin);

            List<IEdge> path = new List<IEdge>();
            path.Add(Origin);
            if (IsMatch(path, Origin))
                return path;

            if (PathCache.ContainsKey(Origin))
            {
                return PathCache[Origin];
            }

            SortedSet<IEdgeKey> untestedEdges = new SortedSet<IEdgeKey>(mesh.AdjacentEdges(Origin));
            untestedEdges.ExceptWith(testedEdges);

            if (untestedEdges.Count == 0)
                return null;
            else if (untestedEdges.Count == 1)
            {
                IEdgeKey adjacentEdge = untestedEdges.First();

                //Check if the face can be part of the path, if not don't bother investigating this route
                if (!CanBePartOfPath(path, mesh[adjacentEdge]))
                {
                    testedEdges.Add(adjacentEdge);
                    return null;
                }

                List<IEdge> result = RecurseEdgePath(ref testedEdges, mesh, adjacentEdge, CanBePartOfPath, IsMatch, PathCache);
                if (result == null)
                    return null;

                path.AddRange(result);
                PathCache[Origin] = path;
                return path;
            }
            else
            {
                List<List<IEdge>> listPotentialPaths = new List<List<IEdge>>(untestedEdges.Count);
                SortedSet<IEdgeKey> AllBranchesTested = new SortedSet<IEdgeKey>();
                foreach (IEdge adjacentEdge in untestedEdges)
                {
                    if (testedEdges.Contains(adjacentEdge))
                        continue;

                    //Check if the face can be part of the path, if not don't bother investigating this route
                    if (!CanBePartOfPath(path, adjacentEdge))
                    {
                        testedEdges.Add(adjacentEdge);
                        continue;
                    }

                    SortedSet<IEdgeKey> testedEdgesCopy = new SortedSet<IEdgeKey>(testedEdges);
                    List<IEdge> result = RecurseEdgePath(ref testedEdgesCopy, mesh, adjacentEdge, CanBePartOfPath, IsMatch, PathCache);
                    if (result == null)
                    {
                        //We know none of the faces lead to the target so don't bother checking them again
                        testedEdges.UnionWith(testedEdgesCopy);
                        continue;
                    }

                    AllBranchesTested.UnionWith(testedEdgesCopy);
                    listPotentialPaths.Add(result);
                }

                //Add the faces we tested so we don't check again
                testedEdges.UnionWith(AllBranchesTested);

                //If no paths lead to destination, return null. 
                if (listPotentialPaths.Count == 0)
                    return null;

                //Otherwise, select the shortest path
                int MinDistance = listPotentialPaths.Select(L => L.Count).Min();
                List<IEdge> shortestPath = listPotentialPaths.Where(L => L.Count == MinDistance).First();
                path.AddRange(shortestPath);
                PathCache[Origin] = path;
                return path;
            }
        }
         
        /// <summary>
        /// Returns a list of edges connected to the passed edge
        /// </summary>
        /// <param name="face"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public IEdge[] AdjacentEdges(IEdgeKey edge)
        {
            List<IEdgeKey> edges = this[edge.A].Edges.Union(this[edge.B].Edges).Distinct().ToList();
            edges.Remove(edge);
            return this[edges].ToArray();
        }
        */
        #endregion


    }
}
