# Command Line Error Handling Improvements

This document describes the improvements made to command line argument parsing error handling across the VikingLegacy codebase.

## Overview

All programs using the CommandLineParser library (version 2.9.1) have been updated to provide better error messages when command line arguments fail to parse. The improvements include:

- Detailed error messages showing specific parsing failures
- Automatic help text display with error context
- Proper exit codes for error conditions
- Consistent error handling across all programs

## Programs Updated

### 1. MonogameTestbed (`Clients/MonogameTestbed/Program.cs`)
- **Before**: Basic error handling with debug break
- **After**: Comprehensive error messages with help text and proper exit codes

### 2. MeasureDistance (`Clients/MeasureDistance/Program.cs`)
- **Before**: Simple "Unable to parse command line arguments, aborting" message
- **After**: Detailed error listing with help text and exit code 1

### 3. Neo4JGenerator (`Servers/Neo4JGenerator/Program.cs`)
- **Before**: Basic error message with comment about automatic help display
- **After**: Full error details with help text and exit code 1

### 4. VikingAU (`Clients/VikingAU/Program.cs`)
- **Before**: Commented out error handling
- **After**: Async error handling with detailed messages and exit code 1

### 5. Viking (`Clients/Viking/Viking/Program.cs`)
- **Before**: Minimal error handling
- **After**: Error messages with fallback to login window

## Implementation Details

### Error Handling Pattern

All programs now follow this consistent pattern:

```csharp
var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
result.WithParsed<CommandLineOptions>(opts => 
{
    // Handle successful parsing
    RunProgram(opts);
})
.WithNotParsed<CommandLineOptions>((errors) => 
{
    // Create a new help text with error information
    var errorHelpText = HelpText.AutoBuild(Parser.Default);
    errorHelpText.AddPreOptionsLine("ERROR: Unable to parse command line arguments.");
    errorHelpText.AddPreOptionsLine("The following errors occurred:");
    
    foreach (var error in errors)
    {
        errorHelpText.AddPreOptionsLine($"  {error}");
    }
    
    errorHelpText.AddPreOptionsLine("");
    Console.WriteLine(errorHelpText);
    
    // Exit with error code
    Environment.Exit(1);
});
```

### Error Types Handled

The CommandLineParser library provides various error types that are now properly displayed:

- **MissingRequiredOptionError**: When a required option is not provided
- **UnknownOptionError**: When an unknown option is specified
- **BadFormatConversionError**: When an option value cannot be converted to the expected type
- **MissingValueOptionError**: When an option that requires a value is provided without one
- **SequenceOutOfRangeError**: When a sequence option has too few or too many values
- **RepeatedOptionError**: When an option is specified multiple times

### Example Error Output

When running a program with invalid arguments, users will now see output like:

```
ERROR: Unable to parse command line arguments.
The following errors occurred:
  Option 'v' is required.
  Option 'invalid-option' is unknown.

Usage: ProgramName [options]
  -v, --VolumeURL=VALUE    URL of VolumeXML file (REQUIRED)
  -u, --username=VALUE     Username (default: Anonymous)
  -p, --password=VALUE     Password (default: connectome)
  -h, --help               Show help
```

## Testing

Two test scripts have been created to demonstrate the improved error handling:

### Batch Script (`test_command_line_errors.bat`)
- Tests all programs with invalid arguments
- Shows the error output for each program
- Windows-specific

### PowerShell Script (`test_command_line_errors.ps1`)
- Cross-platform testing script
- Includes error handling for the test execution
- More detailed output formatting

## Benefits

1. **Better User Experience**: Users get clear, actionable error messages
2. **Consistent Behavior**: All programs handle errors the same way
3. **Proper Exit Codes**: Programs exit with appropriate error codes for automation
4. **Automatic Help**: Help text is always shown with error context
5. **Debugging Support**: Error details help developers identify issues quickly

## Usage

To test the error handling, run either test script:

```bash
# Windows Command Prompt
test_command_line_errors.bat

# PowerShell
.\test_command_line_errors.ps1
```

Or manually test individual programs:

```bash
# Test missing required argument
MonogameTestbed.exe

# Test invalid option
MeasureDistance.exe --invalid-option

# Test wrong data type
Neo4JGenerator.exe --Neo4JDatabase=not-a-url
```

## Future Improvements

Consider these additional enhancements:

1. **Localization**: Support for multiple languages in error messages
2. **Color Output**: Use console colors to highlight errors (where supported)
3. **Logging**: Add structured logging for error conditions
4. **Validation**: Add custom validation rules with specific error messages
5. **Interactive Mode**: Add interactive prompts for missing required options 