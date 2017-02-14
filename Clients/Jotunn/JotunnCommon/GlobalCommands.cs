using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Commands;

namespace Jotunn.Common
{
    static public class GlobalCommands
    {
        public static CompositeCommand IncrementSectionNumber = new CompositeCommand(false);
        public static CompositeCommand DecrementSectionNumber = new CompositeCommand(false); 
    }
}
