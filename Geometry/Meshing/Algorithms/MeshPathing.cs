using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace Geometry.Meshing
{
    public static class DynamicRenderMeshExtensions
    {

        /// <summary>
        /// Returns the shortest path between the starting face and a face meeting a criteria function
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="start">Starting Face</param>
        /// <param name="CanBePartOfPath">Returns true if the tested face can be part of the path.</param>
        /// <param name="MeetsCriteriaFunc">Returns true if the face is the desired destination</param>
        /// <returns></returns>
        public static List<IFace> FindFacesInPath(this Mesh3D mesh, IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc)
        {
            SortedSet<IFace> testedFaces = new SortedSet<IFace>();
            Dictionary<IFace, List<IFace>> PathCache = new Dictionary<IFace, List<IFace>>();
            return RecursePath(ref testedFaces, mesh, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        public static List<IFace> FindFacesInPath(this Mesh3D mesh, IFace start, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> MeetsCriteriaFunc, ref SortedSet<IFace> CheckedFaces)
        {
            Dictionary<IFace, List<IFace>> PathCache = new Dictionary<IFace, List<IFace>>();
            return RecursePath(ref CheckedFaces, mesh, start, CanBePartOfPath, MeetsCriteriaFunc, PathCache);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="testedFaces"></param>
        /// <param name="mesh"></param>
        /// <param name="Origin"></param>
        /// <param name="IsMatch"></param>
        /// <param name="PathCache">Contains a lookup table of the shortest route to the target for each face</param>
        /// <returns></returns>
        private static List<IFace> RecursePath(ref SortedSet<IFace> testedFaces, Mesh3D mesh, IFace Origin, Func<IFace, bool> CanBePartOfPath, Func<IFace, bool> IsMatch, Dictionary<IFace, List<IFace>> PathCache)
        {
            //System.Diagnostics.Trace.WriteLine(Origin.ToString());
            testedFaces.Add(Origin);

            List<IFace> path = new List<IFace>();
            path.Add(Origin);
            if (IsMatch(Origin))
                return path;

            if(PathCache.ContainsKey(Origin))
            {
                return PathCache[Origin];
            }

            SortedSet<IFace> untestedFaces = new SortedSet<IFace>(AdjacentFaces(Origin, mesh));
            untestedFaces.ExceptWith(testedFaces); 

            if (untestedFaces.Count == 0)
                return null;
            else if (untestedFaces.Count == 1)
            {

                IFace adjacentFace = untestedFaces.First();

                //Check if the face can be part of the path, if not don't bother investigating this route
                if(!CanBePartOfPath(adjacentFace))
                {
                    testedFaces.Add(adjacentFace);
                    return null;
                }

                List<IFace> result = RecursePath(ref testedFaces, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
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
                    List<IFace> result = RecursePath(ref testedFacesCopy, mesh, adjacentFace, CanBePartOfPath, IsMatch, PathCache);
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
                List<IFace> shortestPath = listPotentialPaths.Where(L => L.Count == MinDistance).First();
                path.AddRange(shortestPath);
                PathCache[Origin] = path;
                return path;
            }
        }

        public static IFace[] AdjacentFaces(this IFace face, Mesh3D mesh)
        {

            return face.Edges.SelectMany(e => mesh[e].Faces.Where(f => f != face)).ToArray();
        }
    }
}
