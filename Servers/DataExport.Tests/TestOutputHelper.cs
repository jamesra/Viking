namespace DataExport.Tests;

/// <summary>
/// Helper class for organizing test output files into controller-specific folders.
/// </summary>
public static class TestOutputHelper
{
    /// <summary>
    /// Gets the base test output path from configuration or defaults to system temp.
    /// </summary>
    /// <param name="config">Optional configuration to read custom path from TestSettings:TestOutputPath.</param>
    /// <returns>The base path for test outputs.</returns>
    private static string GetBaseTestOutputPath(IConfiguration? config = null)
    {
        string? customPath = config?["TestSettings:TestOutputPath"];
        
        if (!string.IsNullOrEmpty(customPath))
        {
            return Path.Combine(customPath, "DataExport.Tests");
        }
        
        return Path.Combine(Path.GetTempPath(), "DataExport.Tests");
    }
    
    /// <summary>
    /// Gets the full output path for a test file in a controller-specific folder.
    /// </summary>
    /// <param name="controllerName">The name of the controller (e.g., "Network", "Morphology", "Motif").</param>
    /// <param name="fileName">The name of the file including extension.</param>
    /// <param name="config">Optional configuration to read custom test output path.</param>
    /// <returns>The full path where the test output should be saved.</returns>
    public static string GetOutputPath(string controllerName, string fileName, IConfiguration? config = null)
    {
        string baseTestOutputPath = GetBaseTestOutputPath(config);
        string controllerPath = Path.Combine(baseTestOutputPath, controllerName);
        Directory.CreateDirectory(controllerPath);
        return Path.Combine(controllerPath, fileName);
    }
    
    /// <summary>
    /// Gets the full output path for a test file with a timestamp in the filename.
    /// </summary>
    /// <param name="controllerName">The name of the controller (e.g., "Network", "Morphology", "Motif").</param>
    /// <param name="testName">The name of the test.</param>
    /// <param name="extension">The file extension without the dot (e.g., "tlp", "json", "dot").</param>
    /// <param name="config">Optional configuration to read custom test output path.</param>
    /// <returns>The full path where the test output should be saved.</returns>
    public static string GetOutputPath(string controllerName, string testName, string extension, IConfiguration? config = null)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string fileName = $"{testName}-{timestamp}.{extension}";
        return GetOutputPath(controllerName, fileName, config);
    }
    
    /// <summary>
    /// Saves a FileResult to disk, handling different FileResult types.
    /// </summary>
    /// <param name="fileResult">The FileResult to save.</param>
    /// <param name="outputPath">The full path where the file should be saved.</param>
    public static async Task SaveFileResultAsync(FileResult fileResult, string outputPath)
    {
        if (fileResult is FileContentResult contentResult)
        {
            await File.WriteAllBytesAsync(outputPath, contentResult.FileContents);
        }
        else if (fileResult is FileStreamResult streamResult)
        {
            using var fileStream = File.Create(outputPath);
            streamResult.FileStream.Position = 0;
            await streamResult.FileStream.CopyToAsync(fileStream);
        }
        else if (fileResult is PhysicalFileResult physicalResult)
        {
            File.Copy(physicalResult.FileName, outputPath, overwrite: true);
        }
        else
        {
            throw new NotSupportedException($"FileResult type {fileResult.GetType().Name} is not supported for saving.");
        }
    }
}

