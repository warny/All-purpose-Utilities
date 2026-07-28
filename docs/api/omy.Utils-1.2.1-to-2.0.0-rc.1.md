# `omy.Utils` API audit: 1.2.1 to 2.0.0-rc.1

## Method and baseline

The left side is the published `omy.Utils` 1.2.1 `lib/net8.0/Utils.dll` downloaded from NuGet.org. The right side is the Release build of the candidate. Microsoft `ApiCompat` 10.0.302 produced the checked-in [breaking-change output](./omy.Utils-1.2.1-to-2.0.0-rc.1.apicompat.txt) and a [reverse comparison used to inventory additions](./omy.Utils-2.0.0-rc.1-additions.apicompat.txt). The reviewed incompatibilities are pinned in `eng/api-baselines/omy.Utils-1.2.1.xml`; a changed or new incompatibility fails `eng/test-api-compat.ps1`.

## Classification

| Class | Result |
|---|---|
| Compatible | Members absent from both ApiCompat reports retain their binary contract. Existing `net8.0` remains the package TFM. |
| Addition | Date-formula parsing/model APIs, expression compiler/optimizer contracts, formatting builders, math helpers, stream framing options, and other members in the reverse report are additions. |
| Binary break | Removed types/members, newly sealed `Authenticator`, added generic `notnull` constraints, interface-member additions, and assembly version `1.2.1.0` to `2.0.0.0`. |
| Source break | `params T[]` overloads were replaced by sequential `IEnumerable<T>` overloads; removed/relocated namespaces and newly constrained type parameters require caller edits. |
| Behavior change | `DateFormulaConfiguration.json` is now embedded and no longer depends on an application-directory file. Date-formula parsing also exposes provider-based behavior. |
| Removal | The full authoritative list is every `CP0001` and `CP0002` entry in the forward report. Major groups include the old expression parser/builders, number-to-string model, `SkipList<T>`, symbol-tree types, `StringFormat`, and `RandomEx`. |
| Obsolescence | ApiCompat found no new obsolescence transition. Removed APIs were removed directly rather than marked obsolete in this candidate. |

No rename can be proven mechanically. Similar replacements are documented as migrations, not asserted as one-to-one renames. This is intentionally a major-version candidate rather than a patch release.
