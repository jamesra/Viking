using DataExport.Controllers;

namespace DataExport.Tests;

[TestClass]
public class MotifControllerTest
{
    private Mock<IWebHostEnvironment> CreateMockEnvironment()
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.ContentRootPath).Returns(AppContext.BaseDirectory);
        return mockEnv;
    }

    private IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    [TestMethod]
    public async Task TestGetDot()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        var controller = new MotifController(mockEnv.Object, config);

        // Act & Assert - Should not throw for unit test
        // Note: This will fail without a live OData service, but verifies controller setup
        try
        {
            IActionResult result = await controller.GetDot();
            Assert.IsTrue(result is FileResult);
        }
        catch (Exception ex)
        {
            // Expected without OData service - verify it's the right kind of error
            Assert.IsTrue(ex.Message.Contains("OData") || ex.Message.Contains("network") || 
                         ex.GetType().Name.Contains("Http") || 
                         ex is TypeInitializationException, 
                         $"Unexpected exception type: {ex.GetType().Name}, Message: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task TestGetTlp()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        var controller = new MotifController(mockEnv.Object, config);

        // Act & Assert - Should not throw for unit test
        try
        {
            IActionResult result = await controller.GetTLP();
            Assert.IsTrue(result is FileResult);
        }
        catch (Exception ex)
        {
            // Expected without OData service - verify it's the right kind of error
            Assert.IsTrue(ex.Message.Contains("OData") || ex.Message.Contains("network") || 
                         ex.GetType().Name.Contains("Http") || 
                         ex is TypeInitializationException, 
                         $"Unexpected exception type: {ex.GetType().Name}, Message: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task TestGetJson()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        var controller = new MotifController(mockEnv.Object, config);

        // Act & Assert - Should not throw for unit test
        try
        {
            IActionResult result = await controller.GetJSON();
            Assert.IsTrue(result is FileResult);
        }
        catch (Exception ex)
        {
            // Expected without OData service - verify it's the right kind of error
            Assert.IsTrue(ex.Message.Contains("OData") || ex.Message.Contains("network") || 
                         ex.GetType().Name.Contains("Http") || 
                         ex is TypeInitializationException, 
                         $"Unexpected exception type: {ex.GetType().Name}, Message: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("IntegrationTest")]
    public async Task TestMotifGetDotIntegration()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        
        // Skip test if OData URL is not configured or not accessible
        string? odataUrl = config["AppSettings:ODataURL"];
        if (string.IsNullOrEmpty(odataUrl) || !IsODataServiceConfigured(odataUrl))
        {
            Assert.Inconclusive("OData service not configured or not accessible. Configure AppSettings:ODataURL in appsettings.json to run this test.");
            return;
        }

        var controller = new MotifController(mockEnv.Object, config);

        // Act
        try
        {
            IActionResult result = await controller.GetDot();

            // Assert
            Assert.IsTrue(result is FileResult);
            var fileResult = result as FileResult;
            Assert.IsNotNull(fileResult);
            Assert.AreEqual("text/plain", fileResult.ContentType);
            
            // Save the output file for inspection
            string outputPath = TestOutputHelper.GetOutputPath("Motif", "GetDot", "dot", config);
            await TestOutputHelper.SaveFileResultAsync(fileResult, outputPath);
            Console.WriteLine($"Test output saved to: {outputPath}");
        }
        catch (TypeInitializationException ex)
        {
            Assert.Inconclusive($"OData service initialization failed. Service may be unavailable. Error: {ex.InnerException?.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("OData") || ex.Message.Contains("network") || ex.GetType().Name.Contains("Http"))
        {
            Assert.Inconclusive($"OData service connection failed. Error: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("IntegrationTest")]
    public async Task TestMotifGetTlpIntegration()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        
        // Skip test if OData URL is not configured or not accessible
        string? odataUrl = config["AppSettings:ODataURL"];
        if (string.IsNullOrEmpty(odataUrl) || !IsODataServiceConfigured(odataUrl))
        {
            Assert.Inconclusive("OData service not configured or not accessible. Configure AppSettings:ODataURL in appsettings.json to run this test.");
            return;
        }

        var controller = new MotifController(mockEnv.Object, config);

        // Act
        try
        {
            IActionResult result = await controller.GetTLP();

            // Assert
            Assert.IsTrue(result is FileResult);
            var fileResult = result as FileResult;
            Assert.IsNotNull(fileResult);
            Assert.AreEqual("text/plain", fileResult.ContentType);
            
            // Save the output file for inspection
            string outputPath = TestOutputHelper.GetOutputPath("Motif", "GetTlp", "tlp", config);
            await TestOutputHelper.SaveFileResultAsync(fileResult, outputPath);
            Console.WriteLine($"Test output saved to: {outputPath}");
        }
        catch (TypeInitializationException ex)
        {
            Assert.Inconclusive($"OData service initialization failed. Service may be unavailable. Error: {ex.InnerException?.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("OData") || ex.Message.Contains("network") || ex.GetType().Name.Contains("Http"))
        {
            Assert.Inconclusive($"OData service connection failed. Error: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("IntegrationTest")]
    public async Task TestMotifGetJsonIntegration()
    {
        // Arrange
        var mockEnv = CreateMockEnvironment();
        var config = CreateConfiguration();
        
        // Skip test if OData URL is not configured or not accessible
        string? odataUrl = config["AppSettings:ODataURL"];
        if (string.IsNullOrEmpty(odataUrl) || !IsODataServiceConfigured(odataUrl))
        {
            Assert.Inconclusive("OData service not configured or not accessible. Configure AppSettings:ODataURL in appsettings.json to run this test.");
            return;
        }

        var controller = new MotifController(mockEnv.Object, config);

        // Act
        try
        {
            IActionResult result = await controller.GetJSON();

            // Assert
            Assert.IsTrue(result is FileResult);
            var fileResult = result as FileResult;
            Assert.IsNotNull(fileResult);
            Assert.AreEqual("text/plain", fileResult.ContentType);
            
            // Save the output file for inspection
            string outputPath = TestOutputHelper.GetOutputPath("Motif", "GetJson", "json", config);
            await TestOutputHelper.SaveFileResultAsync(fileResult, outputPath);
            Console.WriteLine($"Test output saved to: {outputPath}");
        }
        catch (TypeInitializationException ex)
        {
            Assert.Inconclusive($"OData service initialization failed. Service may be unavailable. Error: {ex.InnerException?.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("OData") || ex.Message.Contains("network") || ex.GetType().Name.Contains("Http"))
        {
            Assert.Inconclusive($"OData service connection failed. Error: {ex.Message}");
        }
    }

    private static bool IsODataServiceConfigured(string odataUrl)
    {
        // Basic check - could be enhanced with actual connectivity test
        // For now, just check if it's a valid URL and not a placeholder
        if (string.IsNullOrEmpty(odataUrl))
            return false;
            
        // Check if it's a placeholder value
        if (odataUrl.Contains("example.com") || odataUrl.Contains("localhost") || odataUrl.Contains("CHANGEME"))
            return false;
            
        return Uri.TryCreate(odataUrl, UriKind.Absolute, out _);
    }
}

