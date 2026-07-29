# Migrating `omy.Utils` 1.2.1 to 2.0.0-rc.1

`2.0.0-rc.1` is a major-version release candidate. Recompile and test every consumer; do not treat it as a drop-in patch.

## Required consumer actions

1. Replace removed expression-parser/builder APIs with the current expression compiler contracts, or retain 1.2.1 until the application can migrate.
2. Move number-to-string usage to the dedicated `omy.Utils.NumberToString` package. The former `Utils.Mathematics` model types are no longer in `omy.Utils`.
3. Replace array-based calls to `FollowedBy`, `PrecededBy`, `Slice`, `BytesExtensions.Join`, and `ObjectUtils.ComputeHash` with their `IEnumerable<T>` forms.
4. Update implementations of changed interfaces (notably `IAngleCalculator<T>`) and satisfy new `notnull` key constraints.
5. Replace removed collection/symbol-tree, formatting, ranges, randomization, reflection, resource, and authenticator members using the current package APIs or application-owned equivalents. Consult the complete linked API audit before upgrading.
6. Remove deployment logic that copied `DateFormulaConfiguration.json`; it is embedded in `omy.Utils` in the candidate.

## Platform and dependencies

The package continues to target `net8.0`. The explicit package dependency is `System.Text.Encoding.CodePages` 9.0.6. The inverted parser project dependency is absent. Nullable annotations are enabled and may introduce new compiler warnings even where the CLR signature remains compatible.

See the [classified API audit](../api/omy.Utils-1.2.1-to-2.0.0-rc.1.md) and validate application behavior before production use.
