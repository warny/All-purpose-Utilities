# Guidelines

Refers to AGENTS.md both in the solution directory and the project direcstory. If the guidelines contradicts themselves, 
the project directory version takes precedence. If the guidelines contradicts the AGENTS.md, the AGENTS.md takes precedence.

# Command execution rules

This project runs primarily on Windows.

The default command environment is **Windows PowerShell 5.1**.

Git, Git Bash and WSL are available, but they must not be used by default when a native Windows or PowerShell solution is appropriate.

The project uses .NET and the `dotnet` CLI.

---

## Shell priority

Use command environments in this order of preference:

1. Windows PowerShell 5.1
2. Native Windows executables called from PowerShell
3. Git called directly from PowerShell
4. Git Bash
5. WSL

Use Git Bash or WSL only when:

* the user explicitly requests them;
* a Bash script must be executed;
* the task requires a genuine Linux environment;
* the relevant tool only exists in Git Bash or WSL;
* the command must reproduce a Unix-based CI or deployment environment;
* the Unix solution is significantly safer or more reliable than the PowerShell equivalent.

Do not switch to Bash or WSL merely because a Unix command is more familiar.

Do not start Git Bash solely to run Git.

Do not start WSL solely to run `git` or `dotnet`.

---

## Shell identification

Before proposing or executing a command:

1. Identify the intended shell.
2. Verify that the syntax is valid for that shell.
3. Verify that the command is compatible with the installed tool version.
4. Do not mix syntax from different shells.

Always label command blocks with their intended environment.

Examples:

**Environment:** Windows PowerShell 5.1

```powershell
Get-ChildItem -Path . -Recurse
```

**Environment:** Git Bash

```bash
find . -type f
```

**Environment:** WSL — Ubuntu

```powershell
wsl --distribution Ubuntu -- bash -lc 'find . -type f'
```

When using Git Bash or WSL, explain why PowerShell is not being used.

---

## Windows PowerShell 5.1 compatibility

All PowerShell commands must be compatible with **Windows PowerShell 5.1** unless another version is explicitly requested.

Do not use PowerShell 7-only syntax or features.

In particular:

* do not use `&&`;
* do not use `||`;
* do not use the null-coalescing operators `??` or `??=`;
* do not use the ternary operator;
* do not assume newer cmdlet parameters exist.

Use:

* `$env:NAME` for environment variables;
* `$null` instead of `/dev/null`;
* `Test-Path` instead of `[ -e ... ]`;
* `Get-ChildItem` instead of Unix-specific `find` or `ls` options;
* `Select-String` instead of `grep`;
* `Get-Content` instead of `cat`;
* `Set-Content`, `Add-Content` or `Out-File` when writing files;
* `Copy-Item` instead of `cp`;
* `Move-Item` instead of `mv`;
* `Remove-Item` instead of `rm`;
* `New-Item -ItemType Directory -Force` instead of `mkdir -p`;
* `Join-Path` when constructing Windows paths.

Quote paths that may contain spaces:

```powershell
Get-Content 'C:\Program Files\MyApplication\config.json'
```

Use the call operator when invoking a path stored in a variable:

```powershell
$executable = 'C:\Program Files\MyTool\tool.exe'
& $executable '--help'
```

---

## Do not mix shell syntax

Never combine PowerShell, Bash, CMD and WSL syntax in the same command unless an explicit shell boundary is required.

Invalid example:

```powershell
export BUILD_MODE=Release && dotnet build
```

Correct PowerShell 5.1 equivalent:

```powershell
$env:BUILD_MODE = 'Release'

dotnet build

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
```

Do not use Bash constructs directly in PowerShell:

* `export NAME=value`;
* `NAME=value command`;
* `$(command)` with Bash semantics;
* `[ -f file ]`;
* `source script.sh`;
* `command1 && command2`;
* `command1 || command2`;
* Unix globbing assumptions;
* Unix redirection syntax when encoding matters.

Do not use PowerShell syntax directly inside Bash.

---

## Native executable rules

Before invoking an executable, determine whether it is:

* a PowerShell cmdlet;
* a PowerShell function;
* a Windows executable;
* a Git command;
* a Git Bash utility;
* a WSL/Linux executable;
* a .NET tool.

Before using an option:

1. Confirm that the option exists.
2. Confirm that it is supported by the installed version.
3. Do not infer options from another tool with a similar name.
4. Do not invent long-form options.
5. Use the local help when uncertain.

Useful help commands:

```powershell
Get-Help CommandName -Full
```

```powershell
tool.exe --help
```

```powershell
tool.exe /?
```

```powershell
git <command> -h
```

```powershell
dotnet <command> --help
```

Do not treat native executable arguments as PowerShell parameters.

Do not assume that GNU arguments are supported by Windows executables.

---

## Command construction

Prefer one clear command per execution step.

Avoid complex one-line commands when a short PowerShell script is safer and easier to validate.

Use variables for paths and intermediate values:

```powershell
$solutionPath = '.\MySolution.sln'
$configuration = 'Release'

dotnet build $solutionPath --configuration $configuration

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
```

For native executables with many arguments, use an argument array:

```powershell
$arguments = @(
    'build'
    '.\MySolution.sln'
    '--configuration'
    'Release'
)

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet failed with exit code $LASTEXITCODE."
}
```

Do not build a single command string and pass it to `Invoke-Expression`.

Do not use `Invoke-Expression` to solve quoting problems.

Do not add unnecessary shell layers such as:

* `powershell -Command`;
* `cmd /c`;
* `bash -c`;
* `sh -c`;
* `wsl bash -c`.

Use an additional shell layer only when the task genuinely requires it.

---

## Error handling

For multi-step PowerShell scripts, use:

```powershell
$ErrorActionPreference = 'Stop'
```

PowerShell cmdlets and native executables do not report failures in the same way.

For PowerShell cmdlets, use exceptions and `-ErrorAction Stop` when appropriate.

For native executables, check `$LASTEXITCODE`:

```powershell
git status

if ($LASTEXITCODE -ne 0) {
    throw "git status failed with exit code $LASTEXITCODE."
}
```

Do not continue after a failed command unless the failure is explicitly expected and handled.

Read the actual standard output and error output before proposing a correction.

Do not repeatedly try random argument variations.

Do not hide errors with broad `try/catch` blocks that ignore the exception.

---

## File paths

Use Windows paths in PowerShell:

```text
C:\Projects\MyApplication
```

Use Linux paths only inside Git Bash or WSL:

```text
/home/user/project
```

Do not use paths such as `/home`, `/tmp` or `/mnt/c` directly in PowerShell unless they are being passed explicitly to WSL.

Do not assume that Windows and WSL use the same working directory.

Do not guess path conversions.

Use `wslpath` when converting between Windows and WSL paths:

```powershell
wsl -- wslpath -a 'C:\Projects\MyApplication'
```

Use an appropriate Windows temporary directory:

```powershell
$tempPath = [System.IO.Path]::GetTempPath()
```

or:

```powershell
$tempPath = $env:TEMP
```

Be careful when Git Bash passes Unix-like paths to Windows executables because Git Bash may automatically rewrite command-line arguments.

---

## Git rules

Git is installed and should normally be called directly from PowerShell.

Preferred usage:

```powershell
git status
git diff
git log --oneline
```

Do not start Git Bash solely to run Git commands.

Before using an unfamiliar Git option:

```powershell
git <command> -h
```

or:

```powershell
git help <command>
```

Do not invent Git options.

Do not assume that the installed Git version supports the latest options.

Before modifying a repository, inspect its state:

```powershell
git status --short --branch
```

When relevant, also inspect:

```powershell
git diff
git diff --staged
```

Potentially destructive Git commands must be shown and explained before execution.

This includes:

* `git reset --hard`;
* `git clean`;
* `git checkout -- <path>`;
* `git restore`;
* `git restore --staged`;
* `git rebase`;
* `git branch -D`;
* `git push --force`;
* `git push --force-with-lease`;
* deleting tags;
* rewriting history.

Do not use `git clean -fdx` without explicit approval.

Do not discard uncommitted changes merely to make a build or checkout succeed.

Do not amend, squash, rebase or force-push unless explicitly requested.

---

## Git Bash rules

Git Bash is available but is not the default shell.

Use it only when:

* executing a Bash script;
* reproducing a Bash-based CI environment;
* using Unix pipelines with no reasonable PowerShell equivalent;
* using a utility bundled specifically with Git Bash.

Do not assume that Git Bash is a complete Linux environment.

Do not assume that every GNU or Linux utility is installed.

Do not assume that package managers such as `apt`, `yum` or `dnf` exist in Git Bash.

When calling Windows executables from Git Bash, be careful with automatic path conversion.

When the command could be run directly in PowerShell with equivalent reliability, prefer PowerShell.

---

## WSL rules

WSL is available but must only be used when a real Linux environment is required.

Make the shell boundary explicit:

```powershell
wsl --distribution Ubuntu -- bash -lc 'command'
```

When the distribution is not known, inspect the available distributions:

```powershell
wsl --list --verbose
```

Do not assume that Ubuntu is the installed or default distribution.

Before running a tool inside WSL, verify that it exists inside that distribution:

```powershell
wsl -- bash -lc 'command -v dotnet'
```

Do not assume that:

* a Windows-installed executable is available inside WSL;
* a WSL-installed executable is available from PowerShell;
* the same .NET SDK is installed in Windows and WSL;
* the same Git configuration is used in Windows and WSL;
* Windows and Linux file permissions behave identically.

Avoid building a project through `/mnt/c` when Linux filesystem behavior or performance is important.

Do not silently run Linux commands through WSL.

Explain why WSL is required.

---

## .NET environment rules

The project uses .NET.

Run the `dotnet` CLI directly from Windows PowerShell 5.1 by default.

Do not start Git Bash or WSL solely to run `dotnet`.

Before running SDK-dependent commands, inspect the environment when the installed SDK is not already known:

```powershell
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes
```

Do not assume that the latest .NET SDK is installed.

Do not assume that the installed runtime includes the corresponding SDK.

Do not assume that the SDK version matches the project's target framework.

Check the selected SDK with:

```powershell
dotnet --version
```

---

## `global.json`

When a `global.json` file exists:

* read it before selecting an SDK;
* respect the requested SDK version;
* respect its roll-forward policy;
* do not modify it merely to make a command succeed;
* report when the requested SDK is not installed.

Checks:

```powershell
if (Test-Path '.\global.json') {
    Get-Content '.\global.json'
}

dotnet --version
dotnet --list-sdks
```

Do not assume that `global.json` is located in the current project directory only. The .NET SDK may discover it in a parent directory.

When SDK selection is unexpected, inspect the current directory and its parent directories.

---

## Project and solution discovery

Do not assume the name or location of a solution or project.

When paths are unknown, locate them first:

```powershell
Get-ChildItem -Path . -Filter '*.sln' -File -Recurse
Get-ChildItem -Path . -Filter '*.slnx' -File -Recurse
Get-ChildItem -Path . -Filter '*.csproj' -File -Recurse
Get-ChildItem -Path . -Filter '*.fsproj' -File -Recurse
Get-ChildItem -Path . -Filter '*.vbproj' -File -Recurse
```

Exclude generated directories such as `bin` and `obj` when a broad recursive search would produce irrelevant results:

```powershell
Get-ChildItem -Path . -Filter '*.csproj' -File -Recurse |
    Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }
```

When several solutions or projects exist, identify the intended target before running:

* build;
* test;
* publish;
* package commands;
* Entity Framework migrations;
* database updates;
* code formatting;
* source generation.

Pass an explicit path when ambiguity is possible:

```powershell
dotnet build '.\src\MyApplication\MyApplication.csproj' `
    --configuration Release
```

Do not rely on the current directory when several buildable targets exist.

---

## Reading project configuration

Before making assumptions about a .NET project, inspect the relevant project file.

Check:

* `TargetFramework`;
* `TargetFrameworks`;
* `RuntimeIdentifier`;
* `RuntimeIdentifiers`;
* `Nullable`;
* `ImplicitUsings`;
* `LangVersion`;
* `OutputType`;
* package references;
* project references;
* conditional property groups;
* custom MSBuild targets;
* SDK declarations.

Distinguish:

```xml
<TargetFramework>net8.0</TargetFramework>
```

from:

```xml
<TargetFrameworks>net8.0;net9.0</TargetFrameworks>
```

Do not assume that all target frameworks can be built with the installed SDK and targeting packs.

For multi-targeted projects, specify the framework when required:

```powershell
dotnet build '.\src\MyLibrary\MyLibrary.csproj' `
    --configuration Release `
    --framework net8.0
```

---

## `dotnet` command validation

Before using an unfamiliar `dotnet` option, inspect the local help:

```powershell
dotnet --help
dotnet build --help
dotnet test --help
dotnet run --help
dotnet publish --help
dotnet restore --help
dotnet pack --help
```

Do not invent `dotnet` options.

Do not assume that an option available in a recent SDK exists in the installed SDK.

Distinguish clearly between:

* `dotnet` command options;
* MSBuild properties;
* compiler options;
* test adapter options;
* application arguments;
* PowerShell parameters.

Examples:

A `dotnet` option:

```powershell
dotnet build '.\MySolution.sln' --configuration Release
```

An MSBuild property:

```powershell
dotnet build '.\MySolution.sln' `
    -p:ContinuousIntegrationBuild=true
```

Application arguments after `--`:

```powershell
dotnet run `
    --project '.\src\MyApplication\MyApplication.csproj' `
    -- `
    --input '.\data\input.json'
```

Do not convert an MSBuild property into an invented long-form `dotnet` option.

---

## Restore

Use an explicit solution or project path:

```powershell
dotnet restore '.\MySolution.sln'
```

`dotnet build`, `dotnet test`, `dotnet run` and `dotnet publish` normally perform an implicit restore.

Do not run a separate restore before every command unless:

* restore diagnostics are needed;
* the workflow explicitly separates restore and build;
* `--no-restore` will be used later;
* CI caching requires a separate restore stage.

Use `--no-restore` only when restore has already completed successfully for the same:

* source tree;
* project configuration;
* SDK;
* package sources;
* lock files;
* target framework;
* runtime identifier.

Do not clear NuGet caches as the first troubleshooting action.

Inspect caches first:

```powershell
dotnet nuget locals all --list
```

Clearing caches is potentially disruptive:

```powershell
dotnet nuget locals all --clear
```

Do not execute it without a concrete reason.

Do not delete `obj\project.assets.json` indiscriminately without first understanding the restore failure.

---

## Build

Use an explicit target and configuration:

```powershell
dotnet build '.\MySolution.sln' `
    --configuration Release
```

Check the result:

```powershell
dotnet build '.\MySolution.sln' `
    --configuration Release

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
```

Do not add `--no-restore` unless restore has already succeeded.

Use diagnostic verbosity only when needed:

```powershell
dotnet build '.\MySolution.sln' `
    --configuration Release `
    --verbosity diagnostic
```

Diagnostic output can be very large. Do not enable it by default.

When a build fails:

1. Preserve the complete error output.
2. Identify the first relevant error.
3. Check the selected SDK.
4. Check `global.json`.
5. Check the working directory.
6. Check the explicit project or solution path.
7. Check the target framework.
8. Check package restore results.
9. Increase verbosity only when necessary.

Do not immediately delete `bin`, `obj`, caches or installed SDKs.

---

## Test

Use an explicit test project or solution:

```powershell
dotnet test '.\tests\MyApplication.Tests\MyApplication.Tests.csproj' `
    --configuration Release
```

When the correct target has already been built:

```powershell
dotnet test '.\tests\MyApplication.Tests\MyApplication.Tests.csproj' `
    --configuration Release `
    --no-build
```

Do not use `--no-build` unless the correct:

* configuration;
* target framework;
* runtime identifier;
* source version

has already been built.

For a test filter:

```powershell
dotnet test '.\tests\MyApplication.Tests\MyApplication.Tests.csproj' `
    --filter 'FullyQualifiedName~Namespace.ClassName'
```

Do not guess filter property names.

Filter syntax can depend on the test platform and adapter.

When uncertain:

```powershell
dotnet test --help
```

Do not assume that xUnit, NUnit and MSTest expose identical filtering behavior.

When generating test result files, specify the logger explicitly:

```powershell
dotnet test '.\tests\MyApplication.Tests\MyApplication.Tests.csproj' `
    --configuration Release `
    --logger 'trx'
```

---

## Run

Prefer an explicit project:

```powershell
dotnet run `
    --project '.\src\MyApplication\MyApplication.csproj'
```

Specify the configuration when relevant:

```powershell
dotnet run `
    --project '.\src\MyApplication\MyApplication.csproj' `
    --configuration Release
```

Use `--` to separate `dotnet run` options from application arguments:

```powershell
dotnet run `
    --project '.\src\MyApplication\MyApplication.csproj' `
    --configuration Release `
    -- `
    --environment Development
```

Do not place application arguments before `--` when they could be interpreted as `dotnet run` options.

Do not assume that setting `ASPNETCORE_ENVIRONMENT` is appropriate for every application.

When needed in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'

dotnet run `
    --project '.\src\Api\Api.csproj'
```

Remember that this environment variable remains set in the current PowerShell process until it is changed or removed.

---

## Publish

Publishing may create, overwrite or remove output files.

Always use an explicit project:

```powershell
dotnet publish '.\src\MyApplication\MyApplication.csproj' `
    --configuration Release `
    --output '.\artifacts\publish'
```

Before publishing, determine whether the deployment must be:

* framework-dependent;
* self-contained;
* runtime-specific;
* portable;
* single-file;
* trimmed;
* ReadyToRun;
* platform-specific.

Do not add publishing properties automatically.

Do not assume that `win-x64` is the correct runtime identifier.

Example of a self-contained Windows publication:

```powershell
dotnet publish '.\src\MyApplication\MyApplication.csproj' `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output '.\artifacts\publish\win-x64'
```

Do not enable trimming automatically. Trimming can break applications that depend on:

* reflection;
* dynamic loading;
* serializers;
* dependency injection scanning;
* COM;
* plugins;
* runtime code generation.

Do not enable single-file publication automatically.

Do not overwrite a deployment directory without identifying its contents and explaining the impact.

---

## Clean

Prefer:

```powershell
dotnet clean '.\MySolution.sln' `
    --configuration Release
```

Do not recursively delete every `bin` and `obj` directory as the first troubleshooting step.

Manual recursive deletion is destructive and must be shown before execution:

```powershell
$directories = Get-ChildItem -Path . -Directory -Recurse |
    Where-Object {
        $_.Name -in @('bin', 'obj')
    }

$directories | Select-Object -ExpandProperty FullName
```

Deletion must be a separate, explicit step:

```powershell
$directories | Remove-Item -Recurse -Force
```

Only use this when `dotnet clean` is insufficient.

Do not delete unrelated directories named `bin` or `obj` without validating their location.

---

## NuGet package rules

Use an explicit project path:

```powershell
dotnet list '.\src\MyApplication\MyApplication.csproj' package
```

Check outdated packages with the syntax supported by the installed SDK:

```powershell
dotnet list '.\src\MyApplication\MyApplication.csproj' package --outdated
```

Before adding or updating a package:

* verify the exact package ID;
* verify the intended version;
* inspect compatibility with the target framework;
* inspect transitive dependency impact;
* check whether central package management is used;
* check whether a lock file is used;
* avoid prerelease versions unless explicitly requested.

Example:

```powershell
dotnet add '.\src\MyApplication\MyApplication.csproj' `
    package 'Microsoft.Extensions.Hosting' `
    --version '8.0.1'
```

Package operations modify project files and may modify:

* `packages.lock.json`;
* `Directory.Packages.props`;
* generated restore files.

Do not use wildcard package versions.

Do not perform broad package updates without showing the scope first.

Check for central package management before changing individual project files:

```powershell
Get-ChildItem -Path . -Filter 'Directory.Packages.props' -File -Recurse
```

---

## .NET tools

Before installing a .NET tool, check whether the repository uses a local tool manifest:

```powershell
Test-Path '.\.config\dotnet-tools.json'
dotnet tool list
dotnet tool list --global
```

Prefer local tools for repository-specific dependencies.

Restore local tools with:

```powershell
dotnet tool restore
```

Do not install a global tool when a local manifest is appropriate.

Do not install or update a global tool without showing the exact command.

Do not assume that a tool command is available merely because its package is referenced by the project.

When using a local tool, prefer its documented invocation.

---

## Entity Framework Core

Do not assume that Entity Framework Core or `dotnet-ef` is installed.

Check local and global tools:

```powershell
dotnet tool list
dotnet tool list --global
```

When a local tool manifest exists:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef -- --help
```

Before creating or applying a migration, identify:

* the project containing the `DbContext`;
* the startup project;
* the target framework;
* the environment;
* the source of the connection string;
* the target database;
* the intended migration name.

Example migration creation:

```powershell
dotnet ef migrations add 'AddCustomerStatus' `
    --project '.\src\Infrastructure\Infrastructure.csproj' `
    --startup-project '.\src\Api\Api.csproj'
```

A database update modifies a database:

```powershell
dotnet ef database update `
    --project '.\src\Infrastructure\Infrastructure.csproj' `
    --startup-project '.\src\Api\Api.csproj'
```

Never execute `database update` without confirming the target database.

Do not assume that the configured connection string points to a local or development database.

For shared, staging or production-like databases, prefer generating a script for review:

```powershell
dotnet ef migrations script `
    --idempotent `
    --project '.\src\Infrastructure\Infrastructure.csproj' `
    --startup-project '.\src\Api\Api.csproj' `
    --output '.\artifacts\migration.sql'
```

Do not include database passwords directly in command-line arguments when they may appear in:

* logs;
* process listings;
* shell history;
* CI output.

---

## MSBuild properties

Pass MSBuild properties explicitly:

```powershell
dotnet build '.\MySolution.sln' `
    --configuration Release `
    -p:ContinuousIntegrationBuild=true
```

Quote properties containing spaces or special characters:

```powershell
dotnet publish '.\src\MyApplication\MyApplication.csproj' `
    '-p:PublishDir=C:\Build Output\Publish\'
```

For complex or repeated build configuration, prefer project files, `Directory.Build.props` or response files instead of an excessively long command line.

Do not treat MSBuild properties as PowerShell named parameters.

Do not expose secrets through `-p:Property=SecretValue`.

Do not assume that a property exists without checking the project or MSBuild documentation.

---

## Formatting commands

Before running a formatting command, verify whether the repository uses:

* `dotnet format`;
* an EditorConfig file;
* analyzers;
* a local tool manifest;
* custom formatting scripts.

Check for configuration:

```powershell
Get-ChildItem -Path . -Filter '.editorconfig' -File -Recurse
Get-ChildItem -Path . -Filter 'dotnet-tools.json' -File -Recurse
```

Formatting may modify many files.

Before applying formatting broadly, prefer a verification mode when supported by the installed version.

Do not assume that every `dotnet format` option exists in every SDK or tool version.

Use local help:

```powershell
dotnet format --help
```

---

## Build outputs and generated files

Do not manually edit generated files unless explicitly required.

Generated files may include:

* files under `obj`;
* source-generator outputs;
* generated NuGet files;
* generated assembly information;
* designer files;
* generated OpenAPI clients;
* generated protobuf or gRPC code.

Identify the source of generation before modifying output files.

Do not commit generated outputs unless the repository convention requires it.

Do not delete generated files without understanding how they are recreated.

---

## Destructive and system-modifying commands

Before executing a command that modifies the system or repository significantly, state:

* the shell;
* the purpose;
* what will change;
* the affected paths, services, repositories or databases;
* required permissions;
* relevant assumptions.

Commands requiring particular caution include:

* deleting files or directories;
* overwriting configuration;
* modifying environment variables permanently;
* modifying the registry;
* changing Windows services;
* changing IIS;
* changing firewall rules;
* modifying certificates;
* changing scheduled tasks;
* installing or uninstalling software;
* installing global .NET tools;
* clearing NuGet caches;
* resetting Git state;
* force-pushing;
* applying Entity Framework migrations;
* modifying databases;
* publishing over an existing deployment.

Show the exact command before destructive execution.

Prefer preview or listing commands before deletion.

---

## Required command response format

When proposing commands, use this structure.

**Environment:** Windows PowerShell 5.1
**Purpose:** Brief description
**Command:**

```powershell
# Command compatible with Windows PowerShell 5.1
```

**Assumptions:**

* required tools;
* expected working directory;
* required permissions;
* relevant versions;
* paths that must exist.

For Git Bash:

**Environment:** Git Bash
**Reason:** Explain why PowerShell is not appropriate.

For WSL:

**Environment:** WSL — distribution name
**Reason:** Explain why a Linux environment is required.

When the command modifies data or configuration, add:

**Impact:** Describe exactly what will be changed.

---

## Failure investigation order

When a command fails:

1. Preserve the exact command.
2. Preserve the complete error message.
3. Check the current shell.
4. Check the working directory.
5. Check that the executable exists.
6. Check the executable version.
7. Inspect the local help.
8. Verify every argument.
9. Verify paths and quoting.
10. Verify permissions.
11. Verify environment variables.
12. Verify the selected .NET SDK when relevant.
13. Verify `global.json` when relevant.
14. Verify the project, solution and target framework.
15. Increase verbosity only when needed.

Do not:

* randomly alter arguments;
* silently switch shells;
* switch to WSL without justification;
* reinstall software immediately;
* delete caches immediately;
* delete `bin` and `obj` immediately;
* modify project files merely to bypass an error;
* claim that a command is valid without verifying it.

If the correct command syntax cannot be confirmed, do not invent it.

Request or retrieve the local help output required to construct the command safely.
