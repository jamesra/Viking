using System;
using System.ComponentModel; 
using System.Collections.Generic;
using System.Collections.Specialized; 
using System.Linq;
using System.Text;
using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; 
using Viking.Common;
using WebAnnotation;
using WebAnnotationModel;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using Viking.Common.UI;
using WebAnnotation.UI.Commands;
using System.Collections.Concurrent;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;

namespace WebAnnotation.ViewModel
{
    public class Location_PropertyPageViewModel : Location_ViewModelBase
    { 
        public Location_PropertyPageViewModel(long locationID)
            : base(locationID)
        {
            
        }
    }
}
