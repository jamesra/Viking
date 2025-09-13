using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Serilog;

namespace Viking.SSL
{
    public static class Certs
    { 
        public static X509Certificate2 LoadSSLCertificate(SSLOptions config)
        {
            X509Certificate2 cert = null;

            // Priority 1: Load from file path (Docker secrets)
            if (!string.IsNullOrEmpty(config.CertificatePath) && File.Exists(config.CertificatePath))
            {
                try
                {
                    if (!string.IsNullOrEmpty(config.Password))
                    {
                        // Load PFX with password using X509Certificate2 constructor
                        cert = X509CertificateLoader.LoadPkcs12FromFile(config.CertificatePath, config.Password);
                    }
                    else
                    {
                        // Load without password
                        cert = X509CertificateLoader.LoadCertificateFromFile(config.CertificatePath);
                    }
                    Log.Information("Loaded certificate from file: {CertificatePath}", config.CertificatePath);
                    return cert;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load certificate from file: {CertificatePath}", config.CertificatePath);
                }
            }

            // Priority 2: Find by DNS name (Subject Alternative Name or Subject)
            if (cert == null && !string.IsNullOrEmpty(config.DnsName))
            {
                try
                {
                    cert = FindCertificateByDnsName(config.DnsName);
                    if (cert != null)
                    {
                        Log.Information("Found certificate by DNS name: {DnsName}", config.DnsName);
                    }
                    return cert;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to find certificate by DNS name: {DnsName}", config.DnsName);
                }
            }

            // Priority 3: Find by serial number (existing functionality)
            if (cert == null && !string.IsNullOrEmpty(config.SerialNumber))
            {
                try
                {
                    cert = FindCertificateBySerialNumber(config.SerialNumber);

                    if (cert != null)
                    {
                        Log.Information("Found certificate by serial number: {SerialNumber}", config.SerialNumber);
                    }

                    return cert;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to find certificate by serial number: {SerialNumber}", config.SerialNumber);
                }
            }

            if (cert != null)
            {
                Log.Information("Successfully loaded certificate. Subject: {Subject}, Thumbprint: {Thumbprint}",
                    cert.Subject, cert.Thumbprint);
            }
            else
            {
                Log.Warning("No valid certificate found.");
            }

            return cert;
        }

        private static X509Certificate2 FindCertificateByDnsName(string dnsName)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                var certificates = store.Certificates
                    .Cast<X509Certificate2>()
                    .Where(cert => cert.NotBefore <= DateTime.UtcNow && cert.NotAfter >= DateTime.UtcNow)
                    .Where(cert => HasDnsName(cert, dnsName))
                    .OrderByDescending(cert => cert.NotAfter)
                    .ToList();

                return certificates.FirstOrDefault();
            }
            finally
            {
                store.Close();
            } 
        }

        private static X509Certificate2 FindCertificateBySerialNumber(string serialNumber)
        {
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                var certificates = store.Certificates
                    .Cast<X509Certificate2>()
                    .Where(cert => cert.NotBefore <= DateTime.UtcNow && cert.NotAfter >= DateTime.UtcNow)
                    .Where(cert => string.Equals(cert.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(cert => cert.NotAfter)
                    .ToList();

                return certificates.FirstOrDefault();
            }
            finally
            {
                store.Close();
            } 
        }

        private static bool HasDnsName(X509Certificate2 cert, string dnsName)
        {
            // Check Subject CN
            if (cert.Subject.Contains($"CN={dnsName}") || cert.Subject.Contains($"CN=*.{dnsName}"))
            {
                return true;
            }

            // Check Subject Alternative Names
            var sanExtension = cert.Extensions.OfType<X509Extension>()
                .FirstOrDefault(e => e.Oid?.FriendlyName == "Subject Alternative Name");

            if (sanExtension != null)
            {
                var sanData = sanExtension.Format(false);
                if (sanData.Contains($"DNS Name={dnsName}") ||
                    sanData.Contains($"DNS Name=*.{dnsName}") ||
                    sanData.Contains($"DNS Name={dnsName},") ||
                    sanData.Contains($"DNS Name=*.{dnsName},"))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
