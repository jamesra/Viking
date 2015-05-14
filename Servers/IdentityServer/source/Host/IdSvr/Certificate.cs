/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Web.Configuration;

namespace VikingIdentityServer
{
    static class Certificate
    {
        /// <summary>
        /// If a serial number is provided we load from the machine store, otherwise use the embedded certificate if it exists
        /// </summary>
        /// <returns></returns>
        public static X509Certificate2 SelectCertificate()
        {
            string X509SerialNumber = WebConfigurationManager.AppSettings.Get("X509SerialNumber");
            if (X509SerialNumber != null && X509SerialNumber.Length > 0)
            {
                return GetFromStore(X509SerialNumber);
            }

            string X509Password = WebConfigurationManager.AppSettings.Get("EmbeddedX509Password");
            return GetFromEmbeddedResource(X509Password);
        }

        public static X509Certificate2 GetFromStore(string serial_number)
        {
            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindBySerialNumber, serial_number, true);
            
            if(col.Count == 0)
            {
                throw new System.ArgumentException("No X509 certificates found with serial number " + serial_number);
            }
            return col[0];
        }

        public static X509Certificate2 GetFromEmbeddedResource(string X509Password)
        {
            var assembly = typeof(Certificate).Assembly;
            using (var stream = assembly.GetManifestResourceStream("IdentityManager.Host.IdSvr.identityserverkey.pfx"))
            {
                return new X509Certificate2(ReadStream(stream), X509Password);
            }
        }

        private static byte[] ReadStream(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}