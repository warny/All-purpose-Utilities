# NTS-08 linguistic source record

This record documents the configurations enabled in the 2026-09-23 NTS-08 slice. It is intentionally narrower than the open audit: languages not listed here remain unsupported unless their existing configuration says otherwise.

| Configuration | Cultures | BaseOn | Ordinals | Implementation | ClockTime | Step / cycle | Variants | Sources | Limitation |
|---|---|---|---|---|---|---|---|---|---|
| EN | EN, EN-us | SCALE-SHORT | Existing | Declarative | Yes | 5 / 12 | none | Cambridge Dictionary, “Telling the time”; British Council LearnEnglish, “How to tell the time” | No day-part wording. `midnight`/`noon` replace the whole wording through `specialHourPattern="{hour}"` (never `noon o'clock`), including after rounding (11:58 → `noon`, 23:58 → `midnight`); with `ReplaceSpecialHours = false` the normal `{hour} o'clock` pattern is used |
| EN-GB | EN-GB, EN-uk | EN | Inherited | Declarative | Inherited (no own section) | 5 / 12 (inherited) | none | Same as EN | Date format remains the only override; same day-part limitation as EN |
| FR | FR, FR-fr, FR-ca | SCALE-LONG | Existing | Declarative | Existing | 5 / 24 | `gender=feminin` through time units | Office québécois de la langue française, Banque de dépannage linguistique, “Heure”; Académie française, “Dire, ne pas dire: Il est huit heures” | Existing project convention retained |
| FR-be | FR-be | FR | Inherited, merged with one override (`80 → quatre-vingtième`) | Declarative | Inherited (no own section) | 5 / 24 (inherited) | inherited gender | Fédération Wallonie-Bruxelles, *Guide de rédaction*; Académie royale de langue et de littérature françaises de Belgique, dictionary resources | Belgian `septante` / `quatre-vingts` / `nonante`, not Swiss `huitante`. ClockTime is deliberately the general French one |
| FR-ch | FR-ch | FR | Inherited (derived from the Swiss cardinals: `huitantième`) | Declarative | Inherited (no own section) | 5 / 24 (inherited) | inherited gender | Dictionnaire suisse romand; TERMDAT (Swiss Federal Chancellery) | Project convention selects `septante` / `huitante` / `nonante` (no cantonal `octante` variant). ClockTime is deliberately the general French one |
| CA | CA, ca-ES | none | Existing | Declarative | Yes | 15 / 12 | `gender=femení` forced on `{hour}` (`una`, `dues`) | Institut d'Estudis Catalans, *Gramàtica essencial de la llengua catalana*, §31.2; Generalitat de Catalunya, Optimot, “expressió de les hores” | Traditional quarter system only (quarters refer to the following hour: `un quart de dues`) |
| CA-valencia | ca-ES-valencia | CA | Inherited (unchanged) | Declarative | Yes, replaces parent | 5 / 12 | `gender=femení` for `1` only (`una`); invariable `dos` for `2-12` in ClockTime only, per AVL priority; display-hour ranges | Acadèmia Valenciana de la Llengua, *Gramàtica normativa valenciana*, §32.2; AVL, *Diccionari normatiu valencià*, entries `quart` and `hora`; AVL glossary, entry `dos` (“l'Acadèmia Valenciana de la Llengua accepta tant l'ús variable com l'invariable d'este numeral [...] però dona prioritat a la forma invariable *dos*”) | No day-part wording. The AVL-prioritized invariable `dos` is scoped to the idiomatic `ConvertClockTime` wording only (`ClockTime` rules); `Convert(TimeOnly)`/`Convert(TimeSpan)`/`ConvertOrdinal` are unmodified and keep the inherited CA `TimeUnits`/`Variants`/`Ordinals` pipeline as-is, so they still gender-agree (`dues hores`, `vint-i-dosena`) - see `Valencian.feature`'s "exact-time wording" scenario |
| ID | ID, ID-ID | SCALE-SHORT | Yes | productive `long` ordinal plugin (`pertama`, otherwise `ke-` + cardinal) | Yes | 5 / 12 | none | Badan Pengembangan dan Pembinaan Bahasa, KBBI entries `pertama`, `lewat`, `setengah`, `kurang`; *Tata Bahasa Baku Bahasa Indonesia* | Neutral convention, no day-part wording. The half hour refers to the following hour (`hourOffset="1"`: 01:30 → `jam setengah dua`). Cardinals keep `delapan`, `miliar`, `triliun`, `kuadriliun`, `kuintiliun` |
| MS | MS, MS-MY | ID | Yes | productive `long` Malay ordinal plugin over the Malay stems (`lapan`, `bilion`, `trilion`, `kuadrilion`, `kuintilion`) | Yes, replaces parent | 5 / 12 | none | Dewan Bahasa dan Pustaka, PRPM entries `lapan`, `pukul`, `suku`, `setengah`, `bilion`, `trilion`, `kuadrilion`, `kuintilion`; DBP, *Tatabahasa Dewan* | Neutral `pukul` convention, no day-part wording. The half hour keeps the current hour (`hourOffset="0"`: 01:30 → `pukul satu setengah`). `NumberScale` overrides only `StaticNames`/`Suffixes` and inherits `startIndex` and the prefix tables from ID through the field-by-field merge |

## Deliberate limitations

These limits are chosen, documented and pinned by tests; they are not oversights:

- **EN / EN-GB**: no day-part marker (`in the morning`, `a.m.`, ...). Only `midnight` and `noon` are special.
- **FR-be / FR-ch**: ClockTime is inherited unchanged from general French; neither child declares a `<ClockTime>` section.
- **CA-valencia**: the AVL-prioritized invariable `dos` is limited to `ConvertClockTime`. `Convert(TimeOnly)`, `Convert(TimeSpan)`, `Variants`, `TimeUnits` and `Ordinals` stay inherited from CA (`dues hores`).
- **ID / MS**: neutral clock convention without day-part wording (`pagi`, `siang`, `petang`, `malam`, ...).

## Keeping the ID/MS ordinal plugins in sync

`IOrdinalLanguageSpecifics` receives no converter, so `IndonesianOrdinalLanguageSpecifics` and `MalayOrdinalLanguageSpecifics` rebuild the cardinal stem themselves. Their stems must stay identical to `NumberConvertionConfiguration.ID.xml` and `NumberConvertionConfiguration.MS.xml`. `NumberToStringRegionalConfigurationTests` checks `ordinal(n) == "ke" + cardinal(n)` for every `n > 1` sample (all stems, every scale up to `long.MaxValue`), and checks that no Indonesian stem appears in a Malay ordinal and no Malay stem appears in an Indonesian ordinal.

## Still open

NTS-08 stays open. BG, CS, DA, FA, NO, RO, SK, SV, SW, TR, UK and ZU (and the other entries listed in `TODO.md`) are out of scope for this record and must not be marked supported until they get the same sourced audit.

## Source links

- Cambridge Dictionary: <https://dictionary.cambridge.org/grammar/british-grammar/time>
- British Council LearnEnglish: <https://learnenglish.britishcouncil.org/grammar/a1-a2-grammar/prepositions-time-at-in-on>
- Office québécois de la langue française: <https://vitrinelinguistique.oqlf.gouv.qc.ca/>
- Académie française: <https://www.academie-francaise.fr/>
- Fédération Wallonie-Bruxelles language service: <https://www.languefrancaise.cfwb.be/>
- TERMDAT: <https://www.termdat.ch/>
- Institut d'Estudis Catalans grammar: <https://geiec.iec.cat/>
- Optimot: <https://aplicacions.llengua.gencat.cat/llc/AppJava/index.html>
- Acadèmia Valenciana de la Llengua publications: <https://www.avl.gva.es/>
- AVL glossary, entry `dos`: <https://www.avl.gva.es/glossari/dos/>
- KBBI: <https://kbbi.kemdikbud.go.id/>
- Dewan Bahasa dan Pustaka PRPM: <https://prpm.dbp.gov.my/>
