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

            Log.Information("Loading SSL certificate. CertificatePath: {CertificatePath}, KeyPath: {KeyPath}, Password: {HasPassword}", 
                config.CertificatePath, config.KeyPath, !string.IsNullOrEmpty(config.Password));

            // Validate KeyPath configuration
            if (!string.IsNullOrEmpty(config.KeyPath) && string.IsNullOrEmpty(config.CertificatePath))
            {
                throw new InvalidOperationException("KeyPath is specified but CertificatePath is empty. Both CertificatePath and KeyPath must be provided when using separate certificate and key files.");
            }

            // Priority 1: Load from file path (Docker secrets)
            if (!string.IsNullOrEmpty(config.CertificatePath) && File.Exists(config.CertificatePath))
            {
                Log.Information("Certificate file exists: {CertificatePath}", config.CertificatePath);
                try
                {
                    if (!string.IsNullOrEmpty(config.KeyPath) && File.Exists(config.KeyPath))
                    {
                        Log.Information("Loading certificate and key from separate files");
                        // Load certificate and key from separate PEM files
                        cert = LoadCertificateWithKey(config.CertificatePath, config.KeyPath);
                        Log.Information("Successfully loaded certificate and key from separate files: Certificate={CertificatePath}, Key={KeyPath}", 
                            config.CertificatePath, config.KeyPath);
                    }
                    else if (!string.IsNullOrEmpty(config.Password))
                    {
                        Log.Information("Loading PFX certificate with password");
                        // Load PFX with password using X509Certificate2 constructor
                        cert = X509CertificateLoader.LoadPkcs12FromFile(config.CertificatePath, config.Password);
                        Log.Information("Successfully loaded PFX certificate with password from file: {CertificatePath}", config.CertificatePath);
                    }
                    else
                    {
                        Log.Information("Loading single certificate file");
                        // Try to load as PFX without password first (it might contain the private key)
                        try
                        {
                            cert = X509CertificateLoader.LoadPkcs12FromFile(config.CertificatePath, null);
                            Log.Information("Successfully loaded certificate from file as PFX: {CertificatePath}, HasPrivateKey: {HasPrivateKey}", 
                                config.CertificatePath, cert?.HasPrivateKey ?? false);
                        }
                        catch (Exception)
                        {
                            // If PFX loading fails, try loading as plain certificate
                            cert = X509CertificateLoader.LoadCertificateFromFile(config.CertificatePath);
                            Log.Information("Successfully loaded certificate from file: {CertificatePath}, HasPrivateKey: {HasPrivateKey}", 
                                config.CertificatePath, cert?.HasPrivateKey ?? false);
                        }
                    }
                    
                    // Log warning if certificate doesn't have private key
                    if (cert != null && !cert.HasPrivateKey)
                    {
                        Log.Warning("Certificate loaded but does not contain a private key. This certificate cannot be used for signing operations. Path: {CertificatePath}", 
                            config.CertificatePath);
                    }
                    
                    return cert;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to load certificate from file: {CertificatePath}", config.CertificatePath);
                }
            }
            else
            {
                Log.Warning("Certificate file not found or path is empty: {CertificatePath}", config.CertificatePath);
            }

            // Priority 2: Find by DNS name (Subject Alternative Name or Subject)
            if (cert == null && !string.IsNullOrEmpty(config.DnsName))
            {
                try
                {
                    cert = FindCertificateByDnsName(config.DnsName);
                    if (cert != null)
                    {
                        Log.Information("Found certificate by DNS name: {DnsName}, HasPrivateKey: {HasPrivateKey}", 
                            config.DnsName, cert.HasPrivateKey);
                        
                        if (!cert.HasPrivateKey)
                        {
                            Log.Warning("Certificate found by DNS name does not have an accessible private key. This certificate cannot be used for signing operations.");
                        }
                    }
                    else{
                        Log.Warning("No certificate found by DNS name: {DnsName}", config.DnsName);
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
                        Log.Information("Found certificate by serial number: {SerialNumber}, HasPrivateKey: {HasPrivateKey}", 
                            config.SerialNumber, cert.HasPrivateKey);
                        
                        if (!cert.HasPrivateKey)
                        {
                            Log.Warning("Certificate found by serial number does not have an accessible private key. This certificate cannot be used for signing operations.");
                        }
                    }
                    else{
                        Log.Warning("No certificate found by serial number: {SerialNumber}", config.SerialNumber);
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
          

        /// <summary>
        /// Loads a certificate and private key from separate PEM files
        /// </summary>
        /// <param name="certificatePath">Path to the certificate PEM file</param>
        /// <param name="keyPath">Path to the private key PEM file</param>
        /// <returns>X509Certificate2 with private key</returns>
        private static X509Certificate2 LoadCertificateWithKey(string publicCertPath, string privateCertPath, string password = null)
        {
            try
            {
                return X509Certificate2.CreateFromPemFile(publicCertPath, privateCertPath);          
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load certificate and key from PEM files. Certificate: {CertificatePath}, Key: {KeyPath}",
                    publicCertPath, privateCertPath);
                throw;
            }
        }

        /// <summary>
        /// Loads a private key from PEM format
        /// </summary>
        /// <param name="keyPem">PEM formatted private key</param>
        /// <returns>RSA private key</returns>
        private static RSA LoadPrivateKeyFromPem(string keyPem)
        {
            try
            {
                // Extract the base64 content from PEM format
                var base64Content = ExtractBase64FromPem(keyPem);
                var keyBytes = Convert.FromBase64String(base64Content);
                
                // Try to load as RSA private key
                if (keyPem.Contains("BEGIN RSA PRIVATE KEY"))
                {
                    var rsa = RSA.Create();
                    rsa.ImportRSAPrivateKey(keyBytes, out _);
                    return rsa;
                }
                // Try to load as PKCS#8 private key
                else if (keyPem.Contains("BEGIN PRIVATE KEY"))
                {
                    var rsa = RSA.Create();
                    rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                    return rsa;
                }
                else
                {
                    throw new NotSupportedException("Unsupported private key format. Expected RSA PRIVATE KEY or PRIVATE KEY PEM format.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load private key from PEM format");
                throw;
            }
        }

        /// <summary>
        /// Extracts base64 content from PEM format
        /// </summary>
        /// <param name="pemContent">PEM formatted content</param>
        /// <returns>Base64 string without headers and whitespace</returns>
        private static string ExtractBase64FromPem(string pemContent)
        {
            // Remove PEM headers and footers
            var lines = pemContent.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("-----"))
                .ToArray();
            
            // Join all lines and remove any remaining whitespace
            return string.Join("", lines).Replace("\r", "").Replace("\n", "").Replace(" ", "");
        }

    }
}
