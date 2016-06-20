using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Geometry.Transforms
{
    public static class Serialization
    { 
        public static IContinuousTransform LoadOrCreateContinuousTransform(string SerializerCacheFullPath, IDiscreteTransform discreteTransform)
        {
            if (discreteTransform as IContinuousTransform != null)
                return discreteTransform as IContinuousTransform;

            ITransformInfo IInfo = discreteTransform as ITransformInfo;
            Geometry.Transforms.StosTransformInfo info = IInfo.Info as Geometry.Transforms.StosTransformInfo;
            ITransformControlPoints IcPoints = discreteTransform as ITransformControlPoints;
            IContinuousTransform continuousTransform = null;

            if (Global.IsCacheFileValid(SerializerCacheFullPath, info.LastModified))
            {
                continuousTransform = LoadSerializedTransformFromCache(SerializerCacheFullPath) as IContinuousTransform;
            }

            if (continuousTransform == null)
            {
                continuousTransform = new DiscreteTransformWithContinuousFallback(discreteTransform,
                                                                                    new RBFTransform(IcPoints.MapPoints, info), info);
                SaveSerializedTransformToCache(SerializerCacheFullPath, continuousTransform);
            }

            return continuousTransform;
        }

        public static ITransform LoadSerializedTransformFromCache(string CacheStosPath)
        {
            try
            {
                using (Stream binFile = System.IO.File.OpenRead(CacheStosPath))
                {
                    var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    object trans = binaryFormatter.Deserialize(binFile);
                    return trans as ITransform; 

                }
            }
            catch (System.Runtime.Serialization.SerializationException e)
            {
                Trace.WriteLine(string.Format("Remove file with Serialization exception {0}\n{1}", e.Message, CacheStosPath));

                System.IO.File.Delete(CacheStosPath);

                return null;
            }
        }

        public static void SaveSerializedTransformToCache(string CacheStosPath, object transform)
        {
            using (Stream binFile = System.IO.File.OpenWrite(CacheStosPath))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(binFile, transform);
            }
        }
    }
}
