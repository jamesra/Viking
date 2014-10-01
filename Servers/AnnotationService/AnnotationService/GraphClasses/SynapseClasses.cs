using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Annotation.Service.GraphClasses
{
    public class SynapseObject
    {
        public List<SynapseStats> objList;

        public SynapseObject()
        {
            objList = new List<SynapseStats>();
        }


    }
    public class SynapseStats
    {
        public string id;
        public string[] synapses;
    }
}
