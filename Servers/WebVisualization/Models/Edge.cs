using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Xml.Linq;
using AnnotationVizLib.AnnotationService;

namespace ConnectomeViz.Models
{
    public class Edge
    {
        public Edge(long SourceID, long TargetID, long SourceParentID, long TargetParentID,
                    StructureLink Link, string SourceTypeName, float factor )
        {
            this.SourceID = SourceID;
            this.TargetID = TargetID;
            this.SourceParentID = SourceParentID;
            this.TargetParentID = TargetParentID;
            this.Link = Link;
            this.SourceTypeName = SourceTypeName;
            this.Strength = factor;
        }

        public long SourceParentID;
        public long TargetParentID;
        public  StructureLink Link;
        public  string SourceTypeName;

        /// <summary>
        /// The number of duplicate connections this edge represents
        /// </summary>
        public float Strength = 1f; 

        public long SourceID
        {
            get;
            set;
        }

        public long TargetID
        {
            get;
            set;
        }

 
        public Edge(long SourceParentID, long TargetParentID, StructureLink link, string SourceTypeName)
        {
            this.SourceParentID = SourceParentID;
            this.TargetParentID = TargetParentID;
            this.Link = link;
            this.SourceTypeName = SourceTypeName; 

        }

        /// <summary>
        /// This string lists the parent structures connected, i.e. cells
        /// </summary>
        public string KeyString
        {
            get
            {
                return SourceParentID + "-" + TargetParentID + "," + SourceTypeName;
            }
        }

        /// <summary>
        /// This string lists the actual structures connection, i.e. synapses and gap junction ID's
        /// </summary>
        public string ConnectionString
        {
            get
            {
                string linkstring = "->";
                if (Link.Bidirectional)
                    linkstring = "<->"; 
                return SourceID + linkstring + TargetID;
            }
        }

        public override int GetHashCode()
        {
            return System.Convert.ToInt32(SourceID);
        }

        public override bool Equals(object obj)
        {
            Edge E = obj as Edge;
            if (E == null)
                return false;

            return SourceID == E.SourceID &&
                   TargetID == E.TargetID; 
        }
    }
}
