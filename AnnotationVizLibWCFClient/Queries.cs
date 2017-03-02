using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AnnotationVizLib.WCFClient.AnnotationService;

namespace AnnotationVizLib.WCFClient
{
    public static class Queries
    {
        public static SortedDictionary<long, StructureType> IDToStructureType
        {
            get
            {
                if (_IDToStructureType == null)
                    _IDToStructureType = Queries.GetStructureTypes();

                return _IDToStructureType; 
            }
        }

        /// <summary>
        /// Ensure the StructureType dictionary is populated
        /// </summary>
        public static void PopulateStructureTypes()
        {
            if (_IDToStructureType == null)
                _IDToStructureType = Queries.GetStructureTypes();
        }

        private static SortedDictionary<long, StructureType> _IDToStructureType = null;

        public static SortedList<string, List<Structure>> LabelToStructuresMap()
        {
            using (AnnotationService.AnnotateStructuresClient client = ConnectionFactory.CreateStructuresClient())
            {
                return LabelToStructuresMap(client);
            }
        }

        public  static SortedList<string, List<Structure>> LabelToStructuresMap(AnnotationService.AnnotateStructuresClient client)
        {
            long typeID =1;
            AnnotationService.Structure[] structures = client.GetStructuresOfType(typeID);

            return LabelToStructuresMap(structures); 
        }

        public static SortedList<string, List<Structure>> LabelToStructuresMap(Structure[] structures)
        {
            SortedList<string, List<Structure>> dictLabels = new SortedList<string, List<Structure>>();
            foreach (Structure structure in structures)
            {
                string Label = BaseLabel(structure.Label);
                if (Label == null)
                    Label = "No Label";

                if (Label.Length == 0)
                    Label = "No Label";

                if (dictLabels.ContainsKey(Label))
                {
                    dictLabels[Label].Add(structure);
                }
                else
                {
                    List<Structure> listIDs = new List<Structure>();
                    listIDs.Add(structure);
                    dictLabels.Add(Label, listIDs);
                }
            }

            return dictLabels; 
        }

        /// <summary>
        /// Handles chunking the request so we do not ask for too much at once
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        public static Structure[] GetStructuresByIDs(long[] IDs, bool include_children=false)
        {
            using(AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                return GetStructuresByIDs(proxy, IDs, include_children);
            }
        }

        /// <summary>
        /// Handles chunking the request so we do not ask for too much at once
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        public static Structure[] GetStructuresByIDs(AnnotateStructuresClient proxy, long[] IDs, bool include_children=false)
        {
            int i = 0;
            int ChunkSize = 4096;
            List<Structure> listStructures = new List<Structure>(IDs.Length);
            while (i < IDs.Length)
            {
                int iEnd = i + ChunkSize < IDs.Length ? i + ChunkSize : IDs.Length;

                long[] subArray = new long[iEnd - i];

                Array.Copy(IDs, i, subArray, 0, iEnd - i);

                listStructures.AddRange(proxy.GetStructuresByIDs(subArray, include_children));

                i = iEnd;
            }

            return listStructures.ToArray();
        }

        /// <summary>
        /// Handles chunking the request so we do not ask for too much at once
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        public static Structure[] GetNetworkedStructures(long[] IDs, int numHops)
        {
            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                long[] network_IDs = proxy.GetNetworkedStructures(IDs, numHops);
                return proxy.GetStructuresByIDs(network_IDs, false);
            }
        }

        /// <summary>
        /// Handles chunking the request so we do not ask for too much at once
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        public static Structure[] GetChildStructuresInNetwork(long[] IDs, int numHops)
        {
            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                return proxy.GetChildStructuresInNetwork(IDs, numHops);
            }
        }

        /// <summary>
        /// Handles chunking the request so we do not ask for too much at once
        /// </summary>
        /// <param name="proxy"></param>
        /// <param name="IDs"></param>
        /// <returns></returns>
        public static StructureLink[] GetStructureLinksInNetwork(long[] IDs, int numHops)
        {
            using (AnnotateStructuresClient proxy = ConnectionFactory.CreateStructuresClient())
            {
                return proxy.GetStructureLinksInNetwork(IDs, numHops);
            }
        }

        public static Scale GetScale()
        {
            using (VolumeMetaClient proxy = ConnectionFactory.CreateVolumeMetaClient())
            {
                Scale scale = proxy.GetScale();
                return scale; 
            }
        }
        

        /***************************************
        * Create structure types dictionary
        * *************************************/
        public static SortedDictionary<long, StructureType> GetStructureTypes()
        {
            using (AnnotateStructureTypesClient proxy = ConnectionFactory.CreateStructureTypesClient())
            { 
                proxy.Open();
                _IDToStructureType = GetStructureTypes(proxy); 
                return _IDToStructureType;
            }
        }

        public static SortedDictionary<long, StructureType> GetStructureTypes(AnnotateStructureTypesClient proxy)
        {
            SortedDictionary<long, StructureType> dictTypes = new SortedDictionary<long, StructureType>();

            StructureType[] StructureTypes = proxy.GetStructureTypes(); 

            foreach (StructureType type in StructureTypes)
            {
                dictTypes.Add(type.ID, type);
            }

            return dictTypes;
        }

        public static SortedDictionary<long, List<StructureLink>> GetLinkedStructures()
        {
            using (AnnotationService.AnnotateStructuresClient client = ConnectionFactory.CreateStructuresClient())
            {
                return GetLinkedStructures(client);
            }

        }

        public static ICollection<long> GetLinkedStructureParentIDs()
        {
            using (AnnotationService.AnnotateStructuresClient client = ConnectionFactory.CreateStructuresClient())
            {
                return GetLinkedStructureParentIDs(client); 
            }
        }
         
        /// <summary>
        /// Return parents of all structures which have a link
        /// </summary>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public static ICollection<long> GetLinkedStructureParentIDs(AnnotateStructuresClient proxy)
        {
            StructureLink[] LinkedStructures = proxy.GetLinkedStructures();
            SortedDictionary<long, List<StructureLink>> StructureToLinkMap = GetLinkedStructures(LinkedStructures);
            Structure[] structures = GetStructuresByIDs(proxy, StructureToLinkMap.Keys.ToArray());

            SortedSet<long> ParentIDs = new SortedSet<long>();
            foreach (Structure linked_struct in structures)
            {
                if (linked_struct.ParentID.HasValue)
                {
                    if (!ParentIDs.Contains(linked_struct.ParentID.Value))
                    {
                        ParentIDs.Add(linked_struct.ParentID.Value);
                    }
                }
            }

            return ParentIDs;
        }

        public static SortedDictionary<long, List<StructureLink>> GetLinkedStructures(AnnotateStructuresClient proxy)
        {
            StructureLink[] LinkedStructures = proxy.GetLinkedStructures();
            return GetLinkedStructures(LinkedStructures); 
        }

        public static SortedDictionary<long, List<StructureLink>> GetLinkedStructures(StructureLink[] LinkedStructures)
        { 
            SortedDictionary<long, List<StructureLink>> StructIDToLinks = new SortedDictionary<long, List<StructureLink>>();
            foreach (StructureLink link in LinkedStructures)
            {
                List<StructureLink> SourceIDList = null;
                if (!StructIDToLinks.TryGetValue(link.SourceID, out SourceIDList))
                {
                    SourceIDList = new List<StructureLink>();
                }

                SourceIDList.Add(link);
                StructIDToLinks[link.SourceID] = SourceIDList;

                List<StructureLink> TargetIDList = null;
                if (!StructIDToLinks.TryGetValue(link.TargetID, out TargetIDList))
                {
                    TargetIDList = new List<StructureLink>();
                }

                TargetIDList.Add(link);
                StructIDToLinks[link.TargetID] = TargetIDList;
            }

            return StructIDToLinks;
        }
        

        /// <summary>
        /// Removes []'ed text and whitespace from a label
        /// </summary>
        /// <param name="label"></param>
        public static string BaseLabel(string Label)
        {
            if (Label == null)
            {
                return "Unlabeled";
            }

            int iBracket = Label.IndexOf('[');

            if (iBracket > 0)
                Label = Label.Substring(0, iBracket - 1);

            Label = Label.Trim();

            if (Label.Length == 0)
            {
                Label = "Unlabeled"; 
            }

            return Label;
        }


    }
}