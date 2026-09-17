# DataExport.Tests

This test project contains comprehensive tests for the DataExport service controllers.

## Test Organization

### Unit Tests
Unit tests validate controller behavior with mocked dependencies. These tests:
- Do not require a live OData service
- Verify controller initialization and basic request handling
- Use mocked `IWebHostEnvironment` and `IConfiguration`
- Expect network/OData exceptions when trying to fetch real data

### Integration Tests
Integration tests validate end-to-end functionality with a live OData service. These tests:
- Are marked with `[TestCategory("IntegrationTest")]`
- **Automatically run** when a valid OData service URL is configured in `appsettings.json`
- **Automatically skip** (marked as Inconclusive) when the OData URL is not configured or appears to be a placeholder
- Require a live OData service connection to pass
- Test actual data retrieval and file generation

## Test Files

- **MotifTest.cs** - Tests for MotifGraph data structures and visualization
- **MotifControllerTest.cs** - Tests for MotifController endpoints (GetDot, GetTLP, GetJSON)
- **MorphologyControllerTest.cs** - Tests for MorphologyController endpoints (GetTLP, GetJSON, PostTLP, PostJSON)
- **NetworkControllerTest.cs** - Tests for NetworkController endpoints (GetDot, GetTLP, GetGML, GetJSON and POST variants)
- **RequestVariablesTest.cs** - Tests for RequestVariables utility class

## Configuration

### Required appsettings.json Settings

**Important:** All tests read configuration directly from the `appsettings.json` file in the test project directory. The configuration is loaded at runtime, not hardcoded.

The test project includes an `appsettings.json` file with the following required configuration:

```json
{
  "AppSettings": {
    "ODataURL": "https://vpn.codepharm.net/RC1Test/OData",
    "VolumeURL": "https://vpn.codepharm.net/RC1Test",
    "XScaleValue": "2.18",
    "YScaleValue": "2.18",
    "ZScaleValue": "90",
    "XScaleUnits": "nm",
    "YScaleUnits": "nm",
    "ZScaleUnits": "nm",
    "DefaultStructureTypeColorsPath": "Resources/ColorMapping/StructureTypeColors.txt",
    "DefaultStructureColorsPath": "Resources/ColorMapping/StructureColors.txt",
    "DefaultLocationColorMapsPath": "Resources/ColorMapping/ImageColorMaps.txt"
  }
}
```

### Configuration Settings Explained

- **ODataURL** - The base URL for the OData service endpoint
- **VolumeURL** - The base URL for the volume/visualization service
- **XScaleValue, YScaleValue, ZScaleValue** - Scale factors for X, Y, Z axes (typically in nanometers)
- **XScaleUnits, YScaleUnits, ZScaleUnits** - Units for scale values (nm, µm, etc.)
- **DefaultStructureTypeColorsPath** - Path to structure type color mapping file
- **DefaultStructureColorsPath** - Path to structure color mapping file
- **DefaultLocationColorMapsPath** - Path to location color maps file

## Running Tests

### Running Unit Tests Only

By default, running all tests will execute only the unit tests (integration tests are ignored):

```bash
dotnet test
```

Or in Visual Studio: Test Explorer → Run All Tests

### Running Integration Tests

Integration tests automatically detect whether they should run based on your configuration:

#### Automatic Behavior

**When OData URL is properly configured:**
- Integration tests will **automatically run** and attempt to connect to the service
- Tests will pass if the service is accessible and returns expected data
- Tests will fail if the service is inaccessible or returns unexpected data

**When OData URL is not configured or is a placeholder:**
- Integration tests will **automatically skip** with status "Inconclusive"
- No attempt is made to connect to any service
- Tests show message: "OData service not configured or not accessible"

#### Configuration for Integration Tests

To enable integration tests, ensure your `appsettings.json` contains a valid OData URL:

```json
{
  "AppSettings": {
    "ODataURL": "https://vpn.codepharm.net/RC1Test/OData"
  }
}
```

**URLs that will skip tests:**
- Empty or null values
- URLs containing "example.com"
- URLs containing "localhost" (if you don't have a local service running)
- URLs containing "CHANGEME"
- Invalid URL formats

#### Running Tests

Run all tests (unit + configured integration tests):
```bash
dotnet test
```

Run only integration tests:
```bash
dotnet test --filter TestCategory=IntegrationTest
```

Run only unit tests (exclude integration):
```bash
dotnet test --filter TestCategory!=IntegrationTest
```

## Test Data

Integration tests use the following test structure IDs from the OData service:

- **Structure IDs**: 180, 476 (for morphology tests)
- **Network ID**: 172 (for network tests)
- **Hops**: 0, 1, 2 (for network graph traversal tests)

These IDs should exist in the target OData service for the tests to pass.

## Required Resources

The following resource files must exist in the `Resources/ColorMapping/` directory:

- `StructureTypeColors.txt` - Maps structure type IDs to colors
- `StructureColors.txt` - Maps structure IDs to colors
- `ImageColorMaps.txt` - Maps image/location IDs to color maps
- PNG files referenced by `ImageColorMaps.txt`

These files are copied to the output directory during build (see `DataExport.Tests.csproj`).

## Troubleshooting

### Common Issues

#### 1. "AppSettings:ODataURL not configured"
**Cause**: The configuration is not loading correctly.

**Solution**: 
- Verify `appsettings.json` exists and is marked as "Copy to Output Directory"
- Check the `DataExport.Tests.csproj` file includes:
  ```xml
  <None Include="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  ```

#### 2. OData Connection Failures (Integration Tests)
**Cause**: Cannot connect to the OData service.

**Solution**:
- Verify the ODataURL is correct in `appsettings.json` and points to an accessible service
- Check VPN connection if required
- Verify network connectivity: `curl https://vpn.codepharm.net/RC1Test/OData`
- If the service is temporarily unavailable, integration tests will show as "Inconclusive"
- For unit tests, network exceptions are expected and handled - they verify controller initialization only

#### 3. Missing Color Mapping Files
**Cause**: Resource files are not copied to the output directory.

**Solution**:
- Verify files exist in `Resources/ColorMapping/`
- Check `DataExport.Tests.csproj` includes:
  ```xml
  <Content Include="Resources\**\*.*" CopyToOutputDirectory="PreserveNewest" />
  ```

#### 4. Test Data Not Found (404 errors in integration tests)
**Cause**: The structure IDs don't exist in the target OData service.

**Solution**:
- Verify the OData service contains structures with IDs 180, 476, and 172
- Update test files with valid structure IDs for your service
- Query the service: `GET https://vpn.codepharm.net/RC1Test/OData/Structures?$filter=ID eq 180`

#### 5. File Access Errors
**Cause**: Output directory permissions or file locks.

**Solution**:
- Ensure the test process has write permissions to the output directory
- Close any files opened by previous test runs
- The `Output/` directory is created automatically by the controllers

## Best Practices

1. **Configure OData URL for Your Environment** - Integration tests auto-run when properly configured
2. **Use Placeholder URLs in Shared Configs** - Set ODataURL to "https://example.com" or "CHANGEME" to skip integration tests by default
3. **Run Unit Tests in CI/CD** - These always run regardless of configuration
4. **Enable Integration Tests in Local Development** - Set a real OData URL in your local `appsettings.json`
5. **Clean Output Directory Periodically** - Integration tests generate files in `Output/` directory
6. **Use Test Filters** - Separate unit and integration test execution with `--filter` parameter

## Additional Notes

- **Unit tests** use exception handling to verify controller initialization without requiring OData access
- **Integration tests** automatically detect configuration and skip if OData URL is not properly configured
- Integration tests expect actual file generation and proper HTTP responses when they run
- The test patterns follow the existing `MotifTest.cs` style for consistency
- All tests use MSTest framework (`[TestClass]`, `[TestMethod]` attributes)
- Configuration checking uses a helper method `IsODataServiceConfigured()` that validates URL format and filters placeholder values

