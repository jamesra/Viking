using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ODataClient.ConnectomeDataModel;

namespace AnnotationVizLib.SimpleOData
{
    class StructureLink : IStructureLink
    {
        public static StructureLink FromDictionary(IDictionary<string, object> dict)
        {
            StructureLink s = new StructureLink { SourceID = System.Convert.ToUInt64(dict["SourceID"]),
                                                  TargetID = System.Convert.ToUInt64(dict["TargetID"]),
                                                  Bidirectional = System.Convert.ToBoolean(dict["Bidirectional"])};
            
            return s;
        }

        public StructureLink()
        {
        }

        public bool Directional
        {
            get
            {
                return !Bidirectional;
            } 
            set { Bidirectional = !value; }
        }

        private bool Bidirectional {get;set;}

        public ulong SourceID
        {
            get; private set;
        }

        public ulong TargetID
        {
            get; private set;
        }

        public override string ToString()
        {
            if(Bidirectional)
                return string.Format("{0} <-> {1}", SourceID, TargetID);
            else
                return string.Format("{0}  -> {1}", SourceID, TargetID);
        }
    }
}
