using AnnotationService.Types;
using System.ServiceModel;

namespace AnnotationService.Interfaces
{
    [ServiceContract]
    interface ICircuit
    {
        [OperationContract]
        Graphx getGraph(int cellID, int numHops);

        [OperationContract]
        long[] getStructuresByTypeID(int typeID);

        [OperationContract]
        string[] getSynapses(int cellID);

        [OperationContract]
        SynapseObject getSynapseStats();

        [OperationContract]
        string[] getTopConnectedStructures(int num);

        [OperationContract]
        string[] getTopConnectedCells();
    }
}
