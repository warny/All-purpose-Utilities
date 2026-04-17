# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed
- Updated the `omy.Utils` NuGet description to a concise consumer-facing summary aligned with the package README and discoverability goals.

### Added
- Refined consumer documentation: updated root README and getting-started guide with complete package inventory, install-first flow, and explicit consumer vs contributor requirements.
- Clarified getting-started and release documentation with csproj-derived TFM guidance, source-generator install example, and explicit CI workflow mapping.
- Added `omy.Utils.Parser` (v0.1.0): self-describing universal parser framework. Tokenizes and parses any ANTLR4 grammar at runtime without code generation. Includes `LexerEngine`, `ParserEngine`, `Antlr4GrammarConverter`, and `RuleResolver`.
- Added XML documentation (English) to all `Utils.Parser` public and private members.
- Added `PackageTags`, `PackageReadmeFile`, `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to `omy.Utils.Parser.csproj`.
- Added `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to all other packable project files.
- Added consumer-focused documentation, getting started guide, GitHub About proposal, and release process notes.
- Marked internal projects (`Utils.Expressions.CLike`, `Utils.Parser.VisualStudio.Worker`) as non-packable to keep NuGet metadata scope limited to published packages.
- Documented package family overview and usage in the root README and base package README.
