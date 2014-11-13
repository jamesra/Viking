using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace ObjectService
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IObjService
    {

        [OperationContract]
        string GetObject(string server, string database, string cell, string type, Boolean update, string virtualPath, string globalPath, string userPath);

       
    }


  
}
