# XHarness Localization Guidelines

XHarness supports localization of user-facing messages through .NET resource files. This enables translation of the CLI into different languages.

## Current Implementation

The XHarness CLI now includes a localization infrastructure that centralizes user-facing strings in resource files.

### What is Localized

- Command descriptions and help text
- Argument descriptions 
- Error messages and validation messages
- Log messages visible to users
- Help system messages

### What is NOT Localized

The following items are intentionally excluded from localization to maintain consistency and functionality:

- Command names and option switches (`apple`, `test`, `--app`, `-v`, etc.)
- Exit code identifiers (`HELP_SHOWN`, `INVALID_ARGUMENTS`, etc.)
- Bundle identifiers and file paths
- Raw device or system logs copied without modification
- Technical identifiers and internal debugging output

## Resource Files

The main resource file is located at:
```
src/Microsoft.DotNet.XHarness.CLI/Resources/Strings.resx
```

This file contains all the localizable strings organized by category:
- `Apple_*` - Apple platform command descriptions
- `Android_*` - Android platform command descriptions  
- `Wasm_*` - WASM platform command descriptions
- `Error_*` - Error message templates
- `Help_*` - Help system messages
- `Arg_*` - Argument descriptions
- `Log_*` - User-visible log messages

## Resource Naming Conventions

Resource keys follow a consistent naming pattern:

1. **Prefix by category**: `Apple_`, `Android_`, `Error_`, `Help_`, `Arg_`, `Log_`
2. **Use descriptive names**: `Apple_Test_Description`, `Error_UnknownArguments`
3. **No spaces**: Use underscores to separate words
4. **Clear context**: Include enough context to understand the usage

Examples:
- `Apple_Test_Description` - Description for the apple test command
- `Error_RequiredArgumentMissing` - Error when a required argument is missing
- `Arg_Target_Description` - Description for the target argument

## Using Localized Strings

### In Command Classes

```csharp
using Microsoft.DotNet.XHarness.CLI.Resources;

internal class AppleTestCommand : AppleAppCommand<AppleTestCommandArguments>
{
    protected override string CommandUsage { get; } = Strings.Apple_Test_Usage;
    protected override string CommandDescription { get; } = Strings.Apple_Test_Description;
    
    public AppleTestCommand(IServiceCollection services) : base("test", false, services, Strings.Apple_Test_Description)
    {
    }
}
```

### In Argument Classes

```csharp
using Microsoft.DotNet.XHarness.CLI.Resources;

internal class AppPathArgument : RequiredPathArgument
{
    public AppPathArgument() : base("app|a=", Strings.Arg_AppPath_Description)
    {
    }
}
```

### In Error Messages

```csharp
throw new ArgumentException(string.Format(Strings.Error_UnknownArguments, string.Join(" ", extraArgs)));
```

## Adding New Localizable Strings

1. **Add to Strings.resx**: Edit the resource file and add new entries following naming conventions
2. **Regenerate Designer class**: The Strings.Designer.cs file should be regenerated automatically, or manually regenerate it
3. **Use in code**: Reference the new string using `Strings.YourNewKey`
4. **Test**: Verify the string appears correctly in the CLI output

## Future Translations

To add support for additional languages:

1. **Create language-specific resource files**: 
   - `Strings.es.resx` for Spanish
   - `Strings.fr.resx` for French  
   - `Strings.de.resx` for German
   - etc.

2. **Translate strings**: Copy all entries from Strings.resx and translate the values

3. **Test with different cultures**: Set the current UI culture and test the CLI

## Testing Localization

The project includes tests to verify localization functionality:

```csharp
// Verify resources can be loaded
Assert.NotNull(Strings.Apple_Test_Description);
Assert.NotEmpty(Strings.Apple_Test_Description);

// Verify error message templates have placeholders
Assert.Contains("{0}", Strings.Error_UnknownArguments);
```

## Best Practices

1. **Keep strings meaningful**: Avoid cryptic abbreviations
2. **Include context**: Make sure translators can understand the usage
3. **Use placeholders correctly**: Follow .NET string formatting conventions (`{0}`, `{1}`, etc.)
4. **Test with longer translations**: Some languages require more space
5. **Avoid cultural assumptions**: Keep messages neutral and professional
6. **Group related strings**: Use consistent prefixes to organize resources

## Localization Architecture

The localization system is built on standard .NET resource management:

- **ResourceManager**: Handles loading resources from assemblies
- **CultureInfo**: Determines which language to use
- **Satellite assemblies**: Would contain translated resources (future)
- **Fallback mechanism**: Falls back to default English if translation missing

This infrastructure allows XHarness to be easily translated into any language supported by .NET while maintaining full functionality and performance.