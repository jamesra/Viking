using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KeyGenerator
{
    /// <summary>
    /// Data Protection Key Generator for Docker Build Process
    /// This utility generates Data Protection keys during Docker image build
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Data Protection Key Generator ===");
            
            // Parse command line arguments
            var keyPath = args.Length > 0 ? args[0] : "/app/DataProtectionKeys";
            var applicationName = args.Length > 1 ? args[1] : "VikingIdentityServer";
            
            Console.WriteLine($"Application: {applicationName}");
            Console.WriteLine($"Key Path: {keyPath}");
            
            try
            {
                GenerateDataProtectionKeys(keyPath, applicationName);
                Console.WriteLine("✅ Data Protection keys generated successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating keys: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static void GenerateDataProtectionKeys(string keyPath, string applicationName)
        {
            // Ensure directory exists
            if (!Directory.Exists(keyPath))
            {
                Directory.CreateDirectory(keyPath);
                Console.WriteLine($"Created directory: {keyPath}");
            }

            // Configure Data Protection services
            var services = new ServiceCollection();
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
                .SetApplicationName(applicationName);

            var serviceProvider = services.BuildServiceProvider();
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();

            // Create a data protector to trigger key generation
            var protector = dataProtectionProvider.CreateProtector("key-generation");
            var testData = protector.Protect("test-data-for-key-generation");

            Console.WriteLine("Data Protection keys generated successfully!");

            // Verify and list the generated key files
            var keyFiles = Directory.GetFiles(keyPath, "*.xml");
            Console.WriteLine($"Generated {keyFiles.Length} key file(s):");
            foreach (var file in keyFiles)
            {
                var fileInfo = new FileInfo(file);
                Console.WriteLine($"  - {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
            }

            // Clean up
            serviceProvider.Dispose();
        }
    }
}
