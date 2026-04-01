# omy.Utils.Parser.VisualStudio

`omy.Utils.Parser.VisualStudio` provides integration services for loading syntax colorization descriptors (`*.syntaxcolor`) and discovering `ISyntaxColorisation` implementations from project and referenced assemblies.

```csharp
var registry = new VisualStudioSyntaxColorisationRegistry();
IReadOnlyList<ISyntaxColorisation> profiles = registry.LoadProfiles(projectAssemblies, descriptorFiles);
```

## Visual Studio classification names (verified)

The default mappings in this package target standard Visual Studio classification names:

- `Keyword`
- `Number`
- `String`
- `Operator`
- `Plain Text`

References:
- https://learn.microsoft.com/visualstudio/extensibility/language-service-and-editor-extension-points
- https://learn.microsoft.com/dotnet/api/microsoft.visualstudio.language.standardclassification.predefinedclassificationtypenames

## Descriptor comments support

`*.syntaxcolor` files support comments:

- full-line comments with `#` or `//`
- trailing comments with `#` or `//`

Example:

```text
@FileExtension : ".demo" // extension
# global comment
Keyword :
    FOR | WHILE # loop keywords
```

Use this package as the runtime layer behind a Visual Studio VSIX classifier/tagger extension.
