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

namespace ConnectomeViz.Models
{
    public class Inter
    {
        public long MainID
        { get; set; }
        public long ID
        { get; set; }
        public long TypeID
        { get; set; }
        public long ChildTypeID
        { get; set; }
        public long ChildStructID
        { get; set; }
        public string Label
        { get; set; }
        public string Dir
        { get; set; }
        public string Name
        { get; set; }
    }
}
