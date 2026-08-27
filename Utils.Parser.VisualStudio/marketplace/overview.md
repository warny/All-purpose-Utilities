# Utils Parser for Visual Studio

Syntax highlighting for custom languages defined with `.syntaxcolor` descriptors, powered by
`Utils.Parser` with isolated out-of-process extensions.

> **Provisional release.** This is a `0.0.x` pre-2.0 build of the extension. Interfaces and
> descriptor formats are expected to remain stable, but this is not yet the first stable `2.0.0`
> build - see [VSIX versioning and release](https://github.com/warny/All-purpose-Utilities/blob/master/docs/releasing/VisualStudioExtension.md).

## What it does

- Loads `*.syntaxcolor` descriptor files from the edited file's folder and parent folders.
- Resolves matching profiles for the current file extension.
- Produces editor classification tags through an out-of-process tagger.
- Forwards classification to user-supplied plugin assemblies running in an isolated worker process.

## Descriptor files

Descriptor files (`.syntaxcolor`) define keyword lists for a given file extension, discovered by
walking from the edited file's directory up to the filesystem root:

```text
@FileExtension : ".demo"

Keyword :
    SELECT | FROM | WHERE

Number :
    NUMBER

String :
    STRING_LITERAL
```

## Plugin system

Users can extend syntax colorization by dropping `ISyntaxColorisation` assemblies into
`%LOCALAPPDATA%\Utils.Parser.VisualStudio\Plugins\`. Plugins always run out-of-process, isolated
from Visual Studio itself, in a sandboxed worker process (Windows AppContainer, Authenticode
signature verification, and several other independent isolation layers - see the full write-up in
the repository README linked below).

## Prerequisites

- Visual Studio 2022 17.14 or later, Community/Professional/Enterprise (`Microsoft.VisualStudio.Component.CoreEditor` component).
- amd64 architecture.

## Links

- Repository and full documentation: <https://github.com/warny/All-purpose-Utilities>
- Extension source and security architecture: <https://github.com/warny/All-purpose-Utilities/blob/master/Utils.Parser.VisualStudio/README.md>
- License: Apache-2.0
- Issues / support: <https://github.com/warny/All-purpose-Utilities/issues>
