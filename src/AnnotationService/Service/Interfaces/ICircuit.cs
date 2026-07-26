using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnnotationService.Types;

namespace AnnotationService.Interfaces
{
    /* Recoded [ServiceContract] */
    interface ICircuit
    {
        /* Recoded [OperationContract] */
        Graphx getGraph(int cellID, int numHops);

        /* Recoded [OperationContract] */
        long[] getStructuresByTypeID(int typeID);

        /* Recoded [OperationContract] */
        string[] getSynapses(int cellID);

        /* Recoded [OperationContract] */
        SynapseObject getSynapseStats();

        /* Recoded [OperationContract] */
        string[] getTopConnectedStructures(int num);

        /* Recoded [OperationContract] */
        string[] getTopConnectedCells();
    }
}

