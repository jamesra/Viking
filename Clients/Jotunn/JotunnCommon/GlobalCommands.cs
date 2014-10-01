using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Prism.Commands;

namespace Jotunn.Common
{
    static public class GlobalCommands
    {
        public static CompositeCommand IncrementSectionNumber = new CompositeCommand();
        public static CompositeCommand DecrementSectionNumber = new CompositeCommand(); 
    }
}
