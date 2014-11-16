using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AnnotationVizLib.AnnotationService;

namespace ConnectomeViz.Helpers
{
    public static class AnnotationQueries
    {
        public static SortedList<string, List<Structure>> LabelToStructuresMap(long typeID)
        {
            using (AnnotateStructuresClient client = ConnectomeViz.Models.State.CreateStructureClient())
            {

                Structure[] structures = client.GetStructuresOfType(typeID);

                //Parse the labels of the structures.
                SortedList<string, List<Structure>> dictLabels = new SortedList<string, List<Structure>>();
                foreach (Structure structure in structures)
                {
                    string Label = BaseLabel(structure.Label);
                    if (Label == null)
                        continue;

                    if (Label.Length == 0)
                        continue;

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
        }

        /***************************************
        * Create structure types dictionary
        * *************************************/ 
        public static SortedDictionary<long, StructureType> GetStructureTypes()
        {
            using (AnnotateStructureTypesClient proxy = ConnectomeViz.Models.State.CreateStructureTypeClient())
            { 
                proxy.Open();
                StructureType[] StructureTypes = proxy.GetStructureTypes();
                proxy.Close();

                SortedDictionary<long, StructureType> dictTypes = new SortedDictionary<long, StructureType>();

                foreach (StructureType type in StructureTypes)
                {
                    dictTypes.Add(type.ID, type);
                }

                return dictTypes;
            }
        }

        /// <summary>
        /// Removes []'ed text and whitespace from a label
        /// </summary>
        /// <param name="label"></param>
        public static string BaseLabel(string Label)
        {
            if(Label == null)
                return Label;

            int iBracket = Label.IndexOf('[');

            if (iBracket > 0)
                Label = Label.Substring(0, iBracket - 1);

            Label = Label.Trim();

            return Label;
        }


    }
}