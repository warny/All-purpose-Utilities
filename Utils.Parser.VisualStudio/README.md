# Utils.Parser.VisualStudio

`Utils.Parser.VisualStudio` is now based on **VisualStudio.Extensibility (out-of-process)**.

## What it does

- Loads `*.syntaxcolor` descriptor files from the edited file folder and parent folders.
- Resolves matching profiles for the current file extension.
- Produces editor `ClassificationTag` tags through an out-of-process `TextViewTagger`.

## Descriptor example

```text
@FileExtension : ".demo"

Keyword :
    SELECT | FROM | WHERE

Number :
    NUMBER

String :
    STRING_LITERAL
```

## Build and debug

1. Build the project.
2. Start debugging the extension from Visual Studio.
3. Open a file matching one of your descriptor extensions.
