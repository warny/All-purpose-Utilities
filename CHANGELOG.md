# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed — `omy.Utils` (dates: the culture's selected calendar)
- `DateFormula` (`Calculate`, `Compile`, `ToExpression`) and `DateUtils.StartOf` / `EndOf` with a `CultureInfo` (and the
  `DayOfWeek` overloads, on the current culture) now use the culture's selected calendar,
  `CultureInfo.DateTimeFormat.Calendar`, instead of its default calendar `CultureInfo.Calendar`; the first day of the
  week was already read from `DateTimeFormat`, and the `DateTimeFormatInfo` overloads already used its calendar. A
  caller can now choose one of the culture's optional calendars on a clone: with `ar-SA` and `UmAlQuraCalendar`,
  `new DateTime(2023, 3, 15).Calculate("NS+1Y", culture)` is 2023-03-23 on every platform, with `GregorianCalendar`
  2023-04-01. Before, the platform's default calendar of the culture always applied (`ar-SA`: Umm al-Qura on Windows,
  Gregorian with the Linux ICU data). Unchanged for a culture whose calendar was not changed; for the current culture
  on Windows, a calendar chosen in the user's regional settings now applies.
- The compiled-formula cache distinguishes the calendar (its type and, for `HijriCalendar`, its `HijriAdjustment`, for
  `GregorianCalendar`, its `CalendarType`) and the first day of the week, so two cultures with the same name but a
  different selection no longer share a cached delegate. A compiled formula (`Compile`, `ToExpression`, the cache) is a
  read-only snapshot of the culture's `DateTimeFormat`: changing the culture or its calendar afterwards no longer
  reaches it.

### Changed — `omy.Utils.NumberToString` (NTS-54, NTS-35 and NTS-34 closed: no open NumberToString item remains)
- **Behavioural change — decimals (NTS-34).** `DecimalReadingMode.LanguageDefault` (the default of `Convert(decimal)`,
  `Convert(double)` and `Convert(decimal, int, …)`) now throws `NotSupportedException` for a non-empty decimal part in
  the languages where no source establishes the automatic digit-by-digit reading, or where the normative reading
  cannot be rendered (declined fraction nouns, the decimal part read as a number): AR, BG, CA, CS, EE, EL, EU, FA, FI,
  GL, HE, HI, HR, HU, ID, MS, IT, JA, KO, NL, PL, PT, RO, RU, SK, SV, SW, TR, UK, VN, WO, ZU and their regional
  cultures, through `decimalDigitByDigitMaxDigits="0"`. Before: `NL` `Convert(21.05m)` = `eenentwintig komma nul vijf`;
  now: `NotSupportedException`. Callers wanting that reading request it: `Convert(21.05m, -1, new DecimalFormatOptions {
  ReadingMode = DecimalReadingMode.DigitByDigit })` returns the same text as before. Integers, `DecimalSuffix` and
  `OmitZeroDecimals` are unaffected. DE, DA, NO, ES and ZH keep their digit-by-digit reading (sourced), FR and EN their
  named fractions. Matrix and sources: `Utils.NumberToString/docs/NTS-34-decimal-reading-audit.md`.
- Romanian (RO, RO-RO): a scale multiplier agrees with its noun (NTS-54). Before *mii* (feminine) the whole multiplier
  is feminine: `douăsprezece mii` (was `doisprezece mii`; DOOM 2 §6.4), `douăzeci și două de mii`, `o sută două mii`,
  `douăzeci și una de mii` (was `douăzeci și unu de mii`; majority spelling, consistent with the existing ordinals).
  Before the neuter *milioane* / *miliarde*: `douăzeci și două de milioane`, `douăsprezece miliarde`, `douăzeci și unu
  de milioane` (unchanged). New value `neutru` of the `gen` dimension (one masculine, two feminine). The `de`
  connector is unchanged.
- Polish (PL): cardinals above 999 999 on the long scale (NTS-35; SJP PWN, Wielki słownik ortograficzny PWN): `jeden
  milion`, `dwa miliony`, `pięć milionów`, `dwadzieścia dwa miliardy`, `jeden bilion`, `jeden biliard`, `jeden
  kwadrylion`, `jeden nonilion`, … without upper bound (`MaxNumber` is now `null`; was 999 999). The ordinals keep
  their validated domain (1–999 999): the million and above still throw `NotSupportedException`.
- Configuration only: no public type or member added or removed.

### Fixed — `omy.Utils.NumberToString` (NTS-53 closed: Catalan, Galician and Romanian numerals)
- Catalan (CA, ca-ES, ca-ES-valencia) writes the plural of *milió* `milions` (IEC, Ortografia catalana §4.4.2.2a: *dos
  milions cent mil*): `dos milions` (was `dos milións`). 10^9 is `mil milions` (DIEC2 "miliard"; was `un mil milió`),
  the multiplier of *milió* being a complete cardinal through the ES/EU `pairedScalesFrom="2"`: `dos mil cinc-cents
  milions` (was `dos mil milións cinc-cents milións`), `mil un milions`, `mil milions d'euros`. The ordinals of the round
  millions are `milionè`, `dos milionè`, `mil milionè`, feminine `milionena` (the IEC attests `milionè` and
  `milionèsim`, GBU §18.5, and prefers the `-è` series, GIEC §17.2.5; were `un milióè`, `dos miliónsè`, `un mil
  milióè`). Under `gender=femení` the multiplier of the masculine *milió* stays masculine (GEIEC §12.3.2): `dos-cents
  milions`, `dos-cents milionena` (was `dues-centes milions`); the thousands keep agreeing (`dues-centes mil`).
- Galician (GL, gl-ES): 10^9 is `mil millóns` (RAG, "eón"; was `un mil millón`), `dous mil cincocentos millóns`, `mil
  millóns de euros`, through the same pairing; under `gender=feminino` the multiplier of *millón* stays masculine:
  `douscentos millóns` (was `douscentas millóns`).
- Romanian (RO, RO-RO) no longer inserts `de` between a scale multiplier ending on one to nineteen and its noun (GALR
  2005): `o sută nouăsprezece mii` (was `o sută nouăsprezece de mii`), `o sută unu milioane`; `o sută douăzeci de mii`
  is unchanged. One before a currency noun takes its attributive form (DEX, "unu"): `un euro`, `un leu și un ban`, `o
  liră` (were `unu euro`, `una liră`); the cardinal `unu` and the compounds (`douăzeci și unu de euro`, `o sută unu
  euro`) are unchanged.
- Added `<ScaleConnector default><Substitution value numberRange numberModulo scales /></ScaleConnector>` (public
  `ScaleConnectorRules`, `ScaleConnectorRule`, `ScaleConnectorType`, `ScaleConnectorSubstitutionType`,
  `LanguageType.ScaleConnectorRules`, `NumberToStringConverterOptions.ScaleConnectorRules`,
  `NumberToStringConverter.ScaleConnectorRules`; additive): the connector between a scale multiplier and its noun,
  decided on the whole multiplier (a paired one included) and the scale; the most specific substitution wins, the
  first declared at equal specificity. The `scaleConnector`/`scaleConnectorThreshold` attributes and options keep their
  meaning and act as its last declared substitution, so Romanian keeps `ScaleConnector == "de"` and
  `ScaleConnectorThreshold == 20`. No other converter's output changed (dump of the 74 converters).

### Fixed — `omy.Utils.NumberToString` (NTS-52 closed: currency noun connectors)
- French (FR, FR-fr, FR-ca, FR-be, FR-ch) introduces the currency noun with `de` after a numeral ending on *million*,
  *milliard* …, elided before a vowel (Académie française, OQLF): `un-million d'euros` (was `un-million euros`),
  `deux-millions d'euros`, `un-million de francs`, `deux-milliards-cinq-cents-millions d'euros`; `un-million-deux-cent-mille
  euros`, `un-million-un euros` are unchanged.
- Catalan (CA, ca-ES, ca-ES-valencia) likewise, elided before a vowel or a mute *h* but not before a consonantal *i*/*u*
  (UOC): `un milió d'euros`, `un milió de pessetes`, `un milió d'hores`, `un milió de iens`.
- Galician (GL, gl-ES) introduces it with `de` (RAG, "millón"): `un millón de euros`, `dous millóns de euros`.
- Romanian (RO, RO-RO) introduces the noun with `de` from twenty, except after a compound ending on one to nineteen
  (GALR 2005): `douăzeci de euro`, `o sută de euro`, `un milion de euro`, `doi lei și douăzeci de bani`; `o sută unu
  euro`, `o sută nouăsprezece euro`, `două milioane trei sute opt lei` keep the noun directly.
- Portuguese is audited only: *um milhão de* is attested (Priberam), but the cardinal domain stops at 999 999, so no
  output changed. Spanish and Italian keep the NTS-51 shorthand, so their output and their public
  `CurrencyTerminalScaleConnector` (`de`, `di`) are unchanged.
- Added `<Currency><NounConnector default nounSeparator><Substitution value nounSeparator terminalScales numberRange
  numberModulo beforeNoun /></NounConnector>` (public `CurrencyNounConnector`, `CurrencyNounConnectorRule`,
  `CurrencyNounConnectorType`, `CurrencyNounConnectorSubstitutionType`, `CurrencyType.NounConnector`,
  `NumberToStringConverterOptions.CurrencyNounConnector`, `NumberToStringConverter.CurrencyNounConnector`; additive): a
  default connector and contextual substitutions; the matching substitution with the most constraints wins, the first
  declared at equal specificity. The NTS-51 `terminalScaleConnector`/`terminalScales` attributes and options remain as
  a shorthand for one substitution; combining them with a `NounConnector` is rejected.

### Fixed — `omy.Utils.NumberToString` (NTS-50 closed: Basque ordinal of zero)
- Basque forms the ordinal of zero, `zerogarren` (EU and eu-ES, previously `NotSupportedException`), listed by
  Euskaltzaindia rule 203 (2025) §5. It is the general rule (the cardinal `zero` + `-garren`); only the domain guard
  changed, now 0–999 999 999 999. No cardinal, fraction, decimal, clock or currency output changed.

### Fixed — `omy.Utils.NumberToString` (NTS-51 closed: Spanish and Italian currency phrases)
- Spanish introduces the currency noun with `de` after a numeral ending on *millón* (DPD "cardinales" §6): `un millón
  de euros` (was `un millón euros`), `dos millones de euros`, `veintiún millones de euros`, `mil millones de euros`,
  `dos mil quinientos millones de euros`; a numeral ending on another cardinal keeps the noun directly: `un millón
  doscientos mil euros`, `un millón un euros`, `mil euros`. Feminine currencies keep the masculine noun: `un millón de
  pesetas`.
- Italian apocopates the currency numeral *uno* alone before a noun taking the article *il* (Treccani, "uno"): `un
  euro` (was `uno euro`), `un euro e un centesimo`; `uno scellino`, `uno zloty`, `uno yen` keep it, and the compounds
  stay invariable: `ventuno euro`, `un milione e uno euro`. Standalone cardinals, ordinals and clocks are unchanged.
- Italian introduces the currency noun with `di` after a numeral ending on *milione*, *miliardo* … (Treccani,
  "milione", "miliardo"): `un milione di euro` (was `un milione euro`), `due miliardi di euro`, `un miliardo e un
  milione di euro`; `un milione e uno euro`, `due milioni e centomila euro`, `mille euro` are unchanged.
- Added to `<Currency>` (public `CurrencyType`, additive): the `terminalScaleConnector` and `terminalScales`
  attributes (`NumberToStringConverterOptions` and `NumberToStringConverter.CurrencyTerminalScaleConnector`,
  `CurrencyTerminalScales`): a connector between the numeral and the noun when the numeral ends, structurally, on a
  listed scale noun; and `<Replacement>` children (public `CurrencyReplacementType`, `CurrencyReplacementRule`,
  `CurrencyReplacements`): replacements of the currency numeral only, optionally blocked before nouns beginning with
  one of `exceptBeforeNoun`. Absent: historical behaviour. A `<Currency>` redeclared through `baseOn` still replaces
  the parent's as a whole.

### Fixed — `omy.Utils.NumberToString` (NTS-30 closed: Vietnamese zero classes, Spanish currency apocope)
- Vietnamese reads a whole zero class between two non-zero classes: `năm mươi triệu không nghìn một trăm linh hai`
  (50 000 102, was `năm mươi triệu một trăm linh hai`), `một triệu không nghìn không trăm linh năm` (SGK Toán 4).
  Trailing zero classes stay silent (`năm mươi triệu`); every composite reusing the cardinal follows (negatives,
  decimals, fractions, currencies). The ordinals of these values are now formed (`thứ một triệu không nghìn không
  trăm linh năm`); two consecutive zero classes, read class by class but not attested, keep a closed ordinal.
- Spanish currency numerals take the attributive apocope of DPD "uno" §2.2: `un euro` (was `uno euro`), `veintiún
  euros`, `treinta y un euros con veintiún céntimos`, for any caller-defined masculine currency; a feminine currency
  declared through `CurrencyDefinition.UnitForcedVariants` reads `una peseta`, `veintiuna pesetas`. Standalone
  cardinals, time units and clocks are unchanged.
- Added the optional `internalZeroGroupString` attribute of `<Language>` (`NumberToStringConverterOptions` and
  `NumberToStringConverter.InternalZeroGroupString`, `LanguageType.InternalZeroGroupString`, additive): the multiplier
  text of a whole zero group between two non-zero groups. Absent: historical behaviour.
- Added the optional `<Currency forceVariants>` element of `<Language>` (public `CurrencyType`,
  `LanguageType.Currency`, `NumberToStringConverterOptions.CurrencyForcedVariants`,
  `NumberToStringConverter.CurrencyForcedVariants`, additive): variants forced on every currency numeral. Precedence:
  caller variants < language currency context < `CurrencyDefinition` forced variants. Absent: historical behaviour.

### Fixed — `omy.Utils.NumberToString` (NTS-38 closed: Spanish and Basque ordinals from the million)
- Basque ordinals follow Euskaltzaindia rule 203 (2025) §5: `milioigarren` (1 000 000, was `milioi batgarren`),
  `bi milioigarren`, `milioi bat eta batgarren`, and are opened to the whole cardinal domain: `mila milioigarren`
  (10^9), `mila milioi eta batgarren`, `bi mila milioigarren` (previously `NotSupportedException`).
- Spanish ordinals are opened from 10^9 after DPD "ordinales" §2d and DLE *milmillonésimo*: `milmillonésimo`,
  `milmillonésimo primero`, feminine `milmillonésima primera`. A multiplier of *millón* written in several words
  (`dos mil millones`, `mil un millones`) still fails closed, like `ciento cincuenta millones`.
- `<OrdinalScale>` reads a round value of the unnamed upper half of a pair (`pairedScalesFrom`) at the named scale
  below it, its multiplier spanning the pair, as the cardinal does. No built-in output changed outside ES and EU.

### Fixed — `omy.Utils.NumberToString` (NTS-33: French ordinals derived from base forms)
- French ordinals are derived from the base of their last constituent instead of the agreed cardinal: `deux centième`
  (was `deux centsième`), `deux millionième` (was `deux millionsième`), `trois cent millionième` (was `trois cents
  millionsième`), `dix milliardième`, `deux billionième`, and `millionième`, `milliardième` for the round scale values
  of one (was `un millionième`, the fraction reading). Belgian French also derives `cent quatre-vingtième` and
  `quatre-vingt millionième` (was `cent quatre-vingtsième`). The feminine keeps a masculine multiplier before the
  masculine scale nouns (`vingt et un millionième`). Cardinals, fractions (`deux centièmes`, `deux millionièmes`),
  decimals, currencies, dates and times are byte-identical; non-round ordinals keep their leading groups cardinal
  (`deux millions unième`). Sources: OQLF (*Écriture des fractions*; *Pluriel de vingt, de cent et de mille*), CCDMD,
  Larousse.
- Added the optional `ordinalBaseString` attribute of `<Digit>` (public `DigitType.OrdinalBaseString`, additive): the
  derivational base used instead of `string` when the digit ends the constituent an ordinal is derived from (the
  lowest group of a cardinal-based ordinal, or the multiplier of an `<OrdinalScale>` value). Absent: historical
  behaviour; an empty or blank value is rejected.

### Fixed — `omy.Utils.NumberToString` (NTS-48: French *vingt*/*cent* agreement)
- French cardinals agree *vingt* and *cent* after the OQLF rule: `quatre-vingts` (80, was `quatre-vingt`),
  `cent quatre-vingts`, `quatre-vingts millions`, and invariable before *mille*: `deux cent mille` (was `deux cents
  mille`), FR-be `quatre-vingt mille` (was `quatre-vingts mille`); `deux cents millions` is unchanged. Fraction
  numerators, decimals, currencies, years and the ordinals built on these cardinals follow (`quatre-vingts
  dix-millièmes`, `deux cent millième`). Configuration only, through the existing `<Groups onScale="1">` tables,
  redeclared by FR-be and FR-ch with their own tens; no engine or API change.

### Changed — `omy.Utils.NumberToString` (NTS-49: French numerals in the 1990 rectified spelling)
- **Output change for every French compound numeral (FR, FR-be, FR-ch).** Every element of a numeral is joined by a
  hyphen, after rule 1 of the 1990 spelling rectifications ("Les numéraux composés sont systématiquement reliés par des
  traits d'union"): `vingt-et-un`, `dix-sept`, `deux-cent-mille`, `deux-millions-trois-cent-mille`, `deux-millionième`,
  `mille-neuf-cent-quatre-vingts`. The words around the numeral keep their spaces (`vingt-cinq millièmes`,
  `vingt-et-un virgule cinq dixièmes`, `moins vingt-deux`, `vingt-et-un euros`). FR 81 is `quatre-vingt-un` (was
  `quatre vingt et un`) and 71 `soixante-et-onze` (was `soixante onze`). Configuration only; no API change.

### Changed — `omy.Utils.NumberToString` (NTS-26 closed by documentation)
- No output change. The Wolof nested multipliers keep the inner genitive `-i` (`ñaari téeméeri milyoŋ`) as a retained
  library convention: the form without it is attested as well (Wolof Ajami Reader, Gaye 1980, Kosogorova 2023), so
  the evidence shows variation, not a defect. See `Utils.NumberToString/docs/NTS-26-wolof-nested-linker-choice.md`.

### Added — `omy.Utils.NumberToString`
- Added the `<OrdinalScale scales multiplierSeparator>` ordinal primitive (XSD `OrdinalScaleType`,
  additive `OrdinalScaleRule` record, `NumberToStringConverterOptions.OrdinalScaleRules`, converter
  `OrdinalScaleRules`): the ordinal of a round scale value is formed on the singular scale noun (the
  multiplier one dropped, a larger multiplier as its own cardinal), then through the usual ordinal
  replacements, stems and variant suffix — never on the assembled cardinal.
- Added the `<OrdinalComposition for divisor separator>` ordinal primitive (XSD
  `OrdinalCompositionType`, additive `OrdinalCompositionRule` record,
  `NumberToStringConverterOptions.OrdinalCompositionRules`, converter `OrdinalCompositionRules`): the
  ordinal of a covered value is ordinal(head) + separator + ordinal(tail), split numerically; each
  part goes through the whole ordinal pipeline with the caller's variants, and adjustment, end
  triggers and finalization run once. Overlapping rules are rejected; `baseOn` replaces the list.
- Added scale lexical forms: `<NumberScale><ScaleForm scale formSelector>` (XSD, `ScaleFormEntry`),
  `NumberToStringConverterOptions.ScaleForms` / `ScaleFormSelectors` and the converter's read-only
  `ScaleForms` / `ScaleFormSelectors`. The existing `ILexicalFormSelector` chooses the form of a
  scale noun from its group multiplier; scales without configuration keep the historical
  singular/plural name. Added `ArabicScaleLexicalFormSelector`.
- Added ordinal-only replacements: `<Replacement>` inside `<Ordinals>` and inside an ordinal
  `<Variant>`, `NumberToStringConverterOptions.OrdinalReplacements`, converter
  `OrdinalReplacements`, and `OrdinalVariantRule.Replacements` through an additional constructor
  overload (the existing constructor is unchanged). They apply to the assembled cardinal before the
  ordinal word/stem/suffix transformation and never affect cardinal conversions.
- Added `ZeroOrdinalUnsupportedLanguageSpecifics`, a shared opt-in guard rejecting the ordinal of
  zero.
- Added the `<OrdinalStem from to>` ordinal primitive (XSD `OrdinalStemType`, additive
  `OrdinalStemRule` record, `NumberToStringConverterOptions.OrdinalStemRules` and converter
  `OrdinalStemRules`): on the suffixed ordinal path the longest matching ending of the last word is
  rewritten before the effective (base or variant) suffix is appended. Exceptions and exact word
  rules keep priority, `removeTrailing` applies unchanged when no stem rule matches, rules are
  validated, snapshotted and sorted once at construction, and `baseOn` merges them by `from`.
- Added the `<Fusion>` morphological composition primitive: `<Digit>` elements (Groups levels 2+)
  and the additive `DigitType.Fusions` / `FusionType` model accept rules (`for`, `removeLeft`,
  `removeRight`, `left`, `right`) that join a digit and its lower sub-group directly, with edge
  changes, instead of through `buildString` (e.g. Italian `venti + uno → ventuno`). Rules are
  cumulative by range specificity, validated strictly at load (conflicts, domain, empty values,
  absent edges, intra-group connector overlap) and compiled into immutable lookup tables.
- Added `ItalianOrdinalLanguageSpecifics`, restricting Italian ordinals to verified forms.
- Added `WolofOrdinalLanguageSpecifics`, a domain guard rejecting the Wolof ordinals of zero and of the round thousands.

### Fixed — `omy.Utils.NumberToString` (NTS-47: last audit of NTS-42)
- Slovak fractions whose numerator ends in a generated `-ón` scale noun after one, which failed closed in the genitive,
  now read its genitive singular in `-óna` (`jedného bilióna tretín`, `jedného trilióna tretín`, `jedného kvadrilióna
  tretín`, up to the engine's own names such as `jedného milikvadrilióna tretín`), after the Pravidlá slovenského
  pravopisu (`bilión`, `trilión`, `kvadrilión`, `kvintilión -a`) and the `dub` declension of the series (Kultúra slova
  2003). Configuration only: one `singular.genitive` key on the SK `<GeneratedScaleForm>`; no public API or XSD change.
- NTS-42 is closed. The Czech and Slovak `lomeno`/`lomené` readings under an external case (grammatical construction
  not established) and the Czech zero numerator in an oblique case (`nula` attested both declined and indeclinable, no
  canonical form) are deliberately unsupported: `ConvertFraction` throws `NotSupportedException` for them by decision,
  documented in `Utils.NumberToString/docs/NTS-42-deliberate-closures.md` and next to the CS and SK language specifics.

### Fixed — `omy.Utils.NumberToString` (NTS-46: NTS-42 E/UK and F/UK closed)
- Ukrainian fractions with a zero numerator, which failed closed in every case, now read `нуль` declined as a noun with
  the denominator in the genitive plural, as after `тисяча` (`нуль третіх`, `нуля третіх`, `нулеві третіх`, `нулем
  третіх`, `нулю третіх`), after a Ukrainian university course (ПНТУ 2015: the part counted by `нуль` is not declined)
  and the VESUM paradigm of `нуль` (first forms).
- Ukrainian fractions whose numerator reaches 10^12, which failed closed in the oblique cases, now decline the generated
  scale nouns (`одним трильйоном третіх`, `двадцяти одному трильйонові третіх`, `двома квадрильйонами трьома
  трильйонами п'ятьма третіми`). The case forms are `singular.<case>` / `plural.<case>` keys of the UK
  `<GeneratedScaleForm>`, read through the internal `IScaleNounSource` (NTS-42 F), so the plugin holds no generated
  name. Cardinals, nominative fractions and every other language are unchanged; no public API or XSD change.

### Fixed — `omy.Utils.NumberToString` (NTS-45: NTS-42 A, B and E/SK closed)
- Czech fractions whose numerator or scale multiplier ends in a compound *jedna* (21, 101, 1001, 21 000, 21 × 10^12),
  which failed closed in the oblique cases and in the *lomeno* reading, now read: the final one is declined with every
  other part and agrees with what it counts, the noun then in the singular of the case (`dvaceti jedné setiny`, `sto
  jednou třetinou`, `dvaceti jednomu tisíci třetin`, `dvaceti jedné miliardě třetin`, `jedna lomeno dvaceti jedním
  tisícem`). The nominative and accusative are unchanged (`dvacet jedna třetin`).
- Slovak zero numerators with a fraction noun, which failed closed in the oblique cases, now keep `nula` and decline the
  noun (`nula tretinám`, `nula tretinami`), after Peciar (Slovenská reč 1958). The Czech zero and the CS/SK slash
  readings under an oblique case stay closed. No public API, XSD or configuration change; no other language changes.

### Fixed — `omy.Utils.NumberToString` (NTS-36 and NTS-42 F closed)
- Czech and Slovak large-scale names now come from Conway tables in their own spelling (intentional output change from
  10^24): CS `quadrilion` → `kvadrilion`, `quintiliarda` → `kvintiliarda`, `octilion` → `oktilion`,
  `quattuordecilion` → `kvatuordecilion`, `quinquagintilion` → `kvinkvagintilion`, `octingentilion` →
  `oktingentilion`; SK likewise with `-ilión` (`kvadrilión`, `kvadriliarda`, `oktilión`). Every other language is
  unchanged.
- Czech and Slovak fractions from 10^12, which failed closed, now decline the generated scale nouns in every case and in
  the slash reading: CS `jednomu bilionu třetin`, `pěti biliardách třetin`, `jedna lomeno dvěma biliony`; SK `jednému
  biliónu tretín`, `dvoma biliónmi tretín`, `jedna lomené jednou biliardou`. The case forms are `singular.<case>` /
  `plural.<case>` keys of the language's `<GeneratedScaleForm>`, read by the built-in plugins through an internal
  interface of the converter, so the plugins hold no Conway name. The Slovak genitive singular of a generated `-ón`
  noun stays closed (lexical ending). No public API or XSD change.

### Fixed — `omy.Utils.NumberToString` (NTS-25B; NTS-25 closed)
- Indonesian, Malay, Swahili and Turkish large-scale names now come from Conway tables adapted to their own
  spelling, with the mechanisms of their attested loans (intentional output change above the forms already in
  use): no `q`, `x` or Latin `c` remains. ID `deciliun` → `desiliun`, `quattuordeciliun` → `kuatuordesiliun`;
  MS `decilion` → `desilion`; SW `quadrilioni` → `kwadrilioni`, `quintilioni` → `kwintilioni`, `sextilioni` →
  `sekstilioni`, `decilioni` → `desilioni`; TR `seksilyon` → `sekstilyon` (corrected), `decilyon` → `desilyon`,
  `duodecilyon` → `dodesilyon`, `quattuordecilyon` → `katordesilyon`, `centilyon` → `sentilyon`. Turkish keeps
  the morphemes of its attested series (`do`, `kator`, `katragint`, `kenkagint`); Malay inherits the Indonesian
  tables. The two-letter Conway link is `ks` (`sekssentiliun`).
- ID and MS keep the junction between Conway groups: `li` + `un` (10^3003 `miliniliun`, was `miniliun`) and
  `li` + `on` (`milinilion`, was `minilion`). Turkish n = 1000 stays `milnilyon` (a `VoidGroup="ini"` variant
  was evaluated and not adopted). No engine, public API or XSD change.

### Fixed — `omy.Utils.NumberToString` (NTS-25A; NTS-25B open)
- Russian, Bulgarian and Ukrainian large scales now use the short scale with a static milliard
  (intentional output change, the previous long scale was wrong for these languages): 10^12 is
  `триллион`/`трилион`/`трильйон` (was `биллион`/`билион`/`більйон`), 10^15 `квадриллион`, 10^18
  `квинтиллион`. Their names above 10^30 come from the new `SCALE-SHORT-CYRILLIC` base, a Cyrillic
  transliteration of the Conway-Guy-Wechsler tables, instead of mixing scripts (`deciллион` →
  `дециллион`, 10^3003 `миллиниллион`, `милинилион`, `мільнільйон`); Bulgarian (`кватуор`) and
  Ukrainian (і/и spelling) override the tables where their orthography differs.
- Swahili and Turkish keep the Conway junction between groups above the 999th -illion: SW splits its
  suffix into `groupSeparator="li"` + `oni` (10^3003 `milinilioni`, was `minilioni`), TR into `l` + `yon`
  (`milnilyon`, was `minilyon`); the names up to the 999th -illion are unchanged. Turkish accusative
  and dative now inflect every generated scale name (`seksilyonu`, `milnilyona`), not only
  `trilyon`/`katrilyon`/`kentilyon`. ID and MS are unchanged (audited in NTS-25B).
- Prefix table entries accept multi-letter linking consonants: a comma-separated `(start)`/`(end)`
  marker list holds one marker per token (`се(кс,с)` + `(н,кс,с)центи` → `сексцентиллион`); without a
  comma each letter remains a marker. Empty tokens are rejected when the scale is built. No public API
  or XSD schema change.

### Fixed — `omy.Utils.NumberToString` (NTS-24 closed)
- Corrected Conway-Wechsler dynamic scale generation (intentional output change): `undecillion`,
  `vigintillion`, `trigintillion` (and every round tens: `quadragintillion` … `nonagintillion`) and the
  grouped names above the 999th -illion now follow the strict Conway-Guy-Wechsler construction: each
  three-digit group restarts from `Scale0Prefixes` (10^3003 `millinillion`, was `unillinillion`;
  10^3000012 `millinillitrillion`), and a single Conway linking consonant is inserted (`trescentillion`,
  `seducentillion`, `sexoctogintacentillion`). `quinquadecillion`, `sedecillion` and `novendecillion` are
  kept as the systematic Conway forms. Languages inheriting `SCALE-SHORT`/`SCALE-LONG` (EN, EE, ID, MS,
  SW, TR; BG, CS, DA, DE, FR, HR, HU, IT, NO/NB, RU, SK, SV, UK and their regional variants) receive the
  corrected names automatically; German also gains `eine` before the corrected `-illion` names.
- Prefix table entries accept a bracketed ending, `(start)stem[default|-illi=>form](end)`, used when the
  component ends its group (`(ns)trigint[a|-illi=>i]`); an unsupported context is rejected when the
  scale is built, as is an entry with text outside the grammar. `FirstLetterUppercase` now capitalizes
  a grouped name once. An empty entry 1–9 in `Scale0Prefixes`, `UnitsPrefixes`, `TensPrefixes` or
  `HundredsPrefixes` makes the scale bounded (`IsUnbounded` false) and the groups needing it
  unnameable instead of producing a degenerate name; ID declares `mi`/`bi` in `Scale0Prefixes`. No public API or XSD schema change.

### Changed — `omy.Utils.NumberToString` (NTS-23 closed)
- Ewe large-number naming now follows the Conway-Wechsler short scale, anchored by sourced `miliɔn`,
  `biliɔn` and `triliɔn` forms and productively extended to higher `-liɔn` names (`quadriliɔn`,
  `quintiliɔn` …). `EE` inherits the `SCALE-SHORT` prefix tables (`baseOn`), spelled prefix + `li` + `ɔn`;
  its `maxNumber` is removed (unbounded `BigInteger` cardinals, ordinals within `long`). Values up to
  999 999 999 are unchanged. Configuration only: no engine, API or XSD change.

### Added — `omy.Utils.NumberToString` (NTS-20)
- Added `<Groups onScale="…">` (`NumberToStringConverterOptions.ScaleScopedGroups`, converter
  `ScaleScopedGroups`, `ScaleScopedGroups` record, `GroupsListType.OnScale`): digit tables rendering the
  multiplier of the covered scales, validated and compiled at load; `baseOn` merges them by range.
- Added `multiplierPosition="beforeScale|afterScale"` (`ScaleMultiplierPosition` enum, options and
  converter property): the scale noun may precede its multiplier.

### Fixed — `omy.Utils.NumberToString` (NTS-20 and NTS-22 closed)
- Ewe cardinals rebuilt on a sourced orthography (`ɖeka`, `etɔ̃`, `atɔ̃`, `adre`, `asieke`, `wuiɖekɛ`,
  `blaeve vɔ ɖekɛ`, `alafa ɖeka`, `akpe ɖeka`, zero `naneke o`) and Ewe ordinals restored (`-lia` on the
  last element, `gbãtɔ` for 1, zero rejected).
- Ewe large-number domain extended: cardinals and ordinals now reach 999 999 999 with the static
  scale name `miliɔn` (`miliɔn ɖeka akpe alafa ɖeka` = 1 100 000); `biliɔn` and higher are not supported.

### Fixed — `omy.Utils.NumberToString` (NTS-19 closed)
- Wolof cardinals follow decree 2005-992 and the grammars: words separated by spaces instead of
  hyphens (`juróom benn`, `ñaar fukk`), the connective `-i` on the multiplier of hundreds and thousands
  (`ñaari téeméer`, `juróom benni téeméer`, `ñaari junni`, `fukki junni`, `téeméeri junni`), `junni`
  alone for 1000 (was `benn junni`), `ak` before the part below the thousands (`junni ak benn`, was
  `benn junni benn`) and zero `tus` (was `sero`). The domain stays 0–999 999.
- Wolof ordinals use the decree's spelling `-éel` (`ñaaréel`, `fukk ak ñaaréel`; was `-eel`) and are
  supported from 1000 (`junni ak bennéel`); only zero and the round thousands (last element `junni`,
  conflicting forms) throw `NotSupportedException`. Ordinals above 999 999 now throw the engine's
  out-of-range `InvalidOperationException`, as in the other languages, instead of
  `NotSupportedException`.

### Fixed — `omy.Utils.NumberToString` (NTS-16 closed)
- Wolof ordinals take the sourced suffix on the last element of the cardinal (`ñaareel`,
  `fukk ak ñaareel`, `ñaar-fukk ak ñenteel`) instead of the unattested `-ël`; `bu njëkk` stays the
  first. Ordinals from 1000 now throw `NotSupportedException`: the thousands cardinal they would be
  built on is not settled (NTS-19). Superseded by NTS-19 above: spelling `-éel`, ordinals from 1000.
- **Behaviour change:** Ewe no longer supports ordinals (`SupportsOrdinals` is `false`,
  `ConvertOrdinal` throws `NotSupportedException`) instead of producing the unattested `etsõ` +
  cardinal (`etsõ gbãtõ`, `etsõ eve`). The sourced `-lia` formation needs the Ewe cardinals to be
  rebuilt first (NTS-20). Cardinal conversions are unchanged for both languages.

### Fixed — `omy.Utils.NumberToString` (NTS-15, NTS-18 closed)
- Italian ordinals of the round multiples of biliardo and trilione are supported (`biliardesimo`,
  `duebiliardesimo`, `trilionesimo`, `duetrilionesimo`, up to `novetrilionesimo` = 9 × 10^18,
  feminine `biliardesima`) instead of failing closed. Configuration only (`<OrdinalScale
  scales="2..6">`); the domain guard now detects the highest scale from a static table without
  overflow up to `long.MaxValue`.
- Italian 1110–1910, the other non-round thousands above 1999 and the non-round values from a
  million still throw `NotSupportedException`, now as a documented deliberate linguistic limitation
  (no canonical form established by the consulted sources) rather than an open item.

### Fixed — `omy.Utils.NumberToString` (NTS-15, NTS-17)
- Italian ordinals of the round multiples of milione, miliardo and bilione are supported
  (`milionesimo`, `duemilionesimo`, `diecimilionesimo`, `miliardesimo`, `bilionesimo`, feminine
  `milionesima`) instead of failing closed; the non-round values from a million and every value from
  a biliardo still fail closed (NTS-18).
- Italian 1010 is `millesimo decimo` and 100001–100009 are `centomillesimoprimo` …
  `centomillesimonono` (Treccani analytic forms) instead of failing closed; 1110–1910 and the other
  non-round thousands above 1999 still fail closed (NTS-15).

### Fixed — `omy.Utils.NumberToString` (NTS-14)
- Italian millions and above are lower-case separate nouns joined by `e` (`un milione`,
  `due milioni e centomila`, `un milione e uno`, `un miliardo`) instead of `uno Millione`; the
  thousands below a million stay soldered. Italian ordinals of a million and above still fail
  closed (NTS-17).
- Arabic thousands take the form their multiplier governs (`ألف`, `ألفان`, `ثلاثة آلاف`,
  `أحد عشر ألفًا`, `مائة ألف`, `مائتا ألف`, `مائة وألف`); a feminine number no longer changes the
  thousands multiplier.
- Hebrew default/standalone ordinals ending in a teen use the masculine teen (`מאה ואחד עשר`).
- The ordinal of zero is `нулевой` (declined) in Russian and `nullte` (declined) in German; Catalan,
  Valencian, Wolof and Ewe now reject it (`NotSupportedException`) instead of producing unsourced
  forms (`zeroè`, `seroël`, `etsõ zero`).

### Fixed — `omy.Utils.NumberToString` (NTS-13)
- Italian compound ordinals are formed from the soldered cardinal (`ventunesimo`, `ventitreesimo`,
  `ventiseiesimo`, `centunesimo`, `centodecimo`, `milleunesimo`, `duemillesimo`, feminine
  `ventunesima`) for 1–1999 except 1010–1910 and the round thousands up to 999000.
  `ItalianOrdinalLanguageSpecifics` is now only a domain guard: zero, 1010–1910, non-round thousands
  above 1999 (NTS-15) and one million and above (NTS-14) still throw
  `NotSupportedException`.

### Fixed — `omy.Utils.NumberToString` (NTS-10, NTS-11, NTS-12)
- Italian compound cardinals are written as one word (`ventuno`, `ventitré`, `centottanta`,
  `duemila`, `milletré`); the Italian clock uses a five-minute step; `diciannovesimo` is fixed and
  unverified compound ordinals now fail closed instead of producing `ventunoesimo`.
- Hindi cardinals 21–99 are lexicalized (`इक्कीस` instead of `बीस एक`).
- Arabic thousands take the attached `و` connector (`ألف وواحد`), and the feminine variant reaches a
  final unit after `و` (`مائة وواحدة`).
- Hebrew cardinals from 1000 are correct (`אלף`, `אלפיים`, `שלושת אלפים`, `אחד עשר אלף`, `ו` before
  the last element only).
- `ConvertOrdinal(0)` no longer returns the cardinal zero unchanged: without an explicit formation it
  throws `NotSupportedException` (PL, ES, GL, PT, EL, HE); Finnish gains `nollas`.
- Added the additive `ClockTimeRule.DisplayHourRange` property and matching
  `displayHourRange` XML/XSD attribute. Clock rules can now vary by the projected hour after each
  candidate rule's offset and the configured hour cycle; construction validates complete,
  non-overlapping coverage and precompiles a 24-by-60 O(1) lookup.
- Added the optional `ClockTimeRule.SpecialHourPattern` property and matching
  `specialHourPattern` XML/XSD attribute. When set, a configured special-hour word (e.g. "midnight",
  "noon") is rendered through this pattern instead of `Pattern`, so special-hour phrasing can differ
  from the numeric-hour phrasing (e.g. dropping an idiomatic "o'clock"); the pattern is validated and
  precompiled the same way as `Pattern`.
- Added independent `ClockTimeRule.HourForcedVariants` and `AmountForcedVariants` init-only
  properties plus the XML attributes `hourForceVariants` and `amountForceVariants`. Clock rules
  now canonicalize aliases at construction, reject dead forcings, preserve caller dimensions,
  correctly treat forced ordinal variants as intentional, and apply the documented precedence
  defaults < caller < time unit < clock rule without changing the positional record constructor.
  A non-empty clock-hour forcing also supersedes a time unit's legacy literal count-one form.
- Added configurable idiomatic clock-face conversion through `ConvertClockTime(TimeOnly)` and
  `ConvertClockTime(DateTime)`, backed by validated `IntRange<int>` minute positions, nearest
  step rounding, configurable hour forms/offsets and reference-based amounts, reusable
  `SpecialHourRule` handling, 12/24-hour numeric projection, variants, XML/XSD support, and
  built-in French and German rules. Clock patterns are strictly validated and precompiled through
  `StringFormatBuilder`, so inserted values are never rescanned as template text; exact-only versus
  whole-hour special-hour semantics and hour-form capabilities are validated consistently.

### Fixed — `omy.Utils.NumberToString`
- Concrete ordinal conversion now throws `NotSupportedException` when `SupportsOrdinals` is
  false, matching the documented interface contract. An `IOrdinalLanguageSpecifics` plugin now
  contributes to `SupportsOrdinals`, including for ordinal clock-hour validation. Plugin-only
  converters also fail closed when the plugin declines a value; plugin-to-XML fallback remains
  available only when a declarative ordinal pipeline is actually configured.
- **`SpecialHourRule`**: generic, per-hour word replacement for time-of-day rendering (e.g.
  "midnight" for hour 0, "noon" for hour 12), configurable via `<SpecialHour hour="..." value="..."
  wholeHour="...">` inside `<TimeUnits>` or programmatically via
  `NumberToStringConverterOptions.SpecialHours`. Not hardcoded to noon/midnight — any hour and any
  language-specific word can be declared. `wholeHour="false"` (default) replaces the hour only
  when minute and second are both zero (sub-second precision is ignored, matching the rest of
  time-of-day rendering); `wholeHour="true"` replaces it for the whole hour, with non-zero
  minutes/seconds still appended. Applies only to `Convert(TimeOnly)` and the time portion of
  `Convert(DateTime)` — never to `Convert(TimeSpan)`, since a duration has no time-of-day meaning.
  New overloads `Convert(TimeOnly, bool replaceSpecialHours, params string[])` and
  `Convert(DateTime, bool replaceSpecialHours, params string[])` opt out per call; the existing
  `params`-only overloads default to `true`. The interface's default implementations of the new
  overloads forward to the existing `params`-only overload (ignoring the flag), so a third-party
  `INumberToStringConverter` written before this feature keeps working unchanged. Wired into the
  built-in EN (`midnight`/`noon`, minute/second-zero instant only) and FR (`minuit`/`midi`, whole
  hour — "midi quinze") configurations.
  **Known minor source-compat gap:** a call site written as `Convert(time, default)` — an untyped
  `default` literal in place of `variants` — becomes an ambiguous overload call (`CS0121`) now
  that the `bool replaceSpecialHours` overload exists, since the untyped `default` literal
  converts equally well to `bool` and `string[]`. Binary compatibility is unaffected; source call
  sites using this pattern should switch to `[]` or omit `variants` — **not** `default(string[])`,
  which compiles but passes a `null` array that crashes with `NullReferenceException` in variant
  handling.

## [2.0.0-rc.2] - Release candidate

Compatibility baseline for this candidate moves forward to the published `2.0.0-rc.1` (not the
legacy 1.x/0.x packages `2.0.0-rc.1` itself was compared against) - see
[`eng/api-breaking-changes/2.0.0-rc.2.json`](eng/api-breaking-changes/2.0.0-rc.2.json) for the
exact accepted RC1&rarr;RC2 diagnostics. No public API changed relative to `2.0.0-rc.1` for any
manifested package as of this candidate.

## [2.0.0-rc.1] - Release candidate

### `omy.Utils.Fonts`

- **Breaking:** hardened SFNT/TrueType parsing against hostile input (second quality/security audit
  pass, items 21-39 of `Utils.Fonts/TODO-2026-07-19-pass2.md`). Unsigned wire types throughout
  (`numTables`, table offsets/lengths/checksums, `loca` Offset16/32, `cmap` counts/offsets, composite
  glyph indices); new `TrueTypeFontParsingOptions`/`FontValidationMode` (strict/permissive) with
  structured `FontDiagnostic`s and `FontParseException`; bounded resource limits (font/table size,
  table count, `cmap` subtable count, composite glyph depth/components/points) enforced before
  allocation; SFNT directory entries preserved in an ordered list instead of a lossy `SortedSet`
  (duplicate tags, aliases, and overlaps are now detected and policy-driven instead of silently
  dropped); read-only table/font checksum computation instead of temporarily zeroing bytes in the
  source stream; bounded per-table stream slices instead of eagerly duplicating every table into a
  fresh buffer; cycle- and budget-guarded compound glyph resolution; immutable `CmapTable.CMaps` and
  `GlyphCompound.Instructions`.
- **Breaking:** `GlyphCompound.getGlyphIndex(int)` renamed to `GetGlyphIndex(int)` and returns
  `ushort`; `GlyphCompound.Instructions` is `ReadOnlyMemory<byte>` instead of `byte[]`;
  `CmapTable.CMaps` is `IReadOnlyList<CMapFormatBase>` instead of an array; several `short`-typed
  fields widened to `ushort`/`int` (`TrueTypeFont.TablesCount`/`SearchRange`/`EntrySelector`/
  `RangeShift`, `CmapTable.Version`/`NumberSubtables`, `GlyphBase.Length`).
- New: `TrueTypeFont.ParseFont`/`ParseFontAsync` overloads taking `TrueTypeFontParsingOptions`;
  `TrueTypeFont.Diagnostics`; `WriteFont(Stream, TrueTypeFontWritingOptions)`/`WriteFontAsync`.
- Preview language features disabled and `LangVersion` pinned for the package (was not actually
  relying on any preview-only feature).
- See `docs/releasing/MigrationTo2.0.md` and `docs/releasing/AcceptedApiBreaks.md#omy-utils-fonts`.

### `omy.Utils`

- **Breaking:** removed the legacy expression parser/builders, number-to-string model, `SkipList<T>`, symbol tree, `StringFormat`, `RandomEx`, and the removed members enumerated in the [1.2.1 API audit](docs/api/omy.Utils-1.2.1-to-2.0.0-rc.1.md). Some sequential `params` array overloads now accept `IEnumerable<T>`, generic dictionary keys gained `notnull` constraints, `Authenticator` is sealed, and interfaces such as `IAngleCalculator<T>` gained members.
- Nullability annotations are enabled; consumers can receive new source warnings and must recompile.
- `DateFormulaConfiguration.json` is now an embedded resource, so consumers must stop deploying an application-directory copy.
- The package remains `net8.0`, depends on `System.Text.Encoding.CodePages` 9.0.6, and no longer carries the inverted parser project dependency.
- Follow the [migration guide](docs/migration/omy.Utils-1.2.1-to-2.0.0-rc.1.md); these changes justify the major version from published 1.2.1.

### `omy.Utils.Parser` and associated packages

- Established the first API baselines for Source, Diagnostics, Antlr4.Common, Parser, Expressions, and Generators.
- Synchronized all parser packages and `omy.Utils` at exactly `2.0.0-rc.1`, including exact internal NuGet dependencies.
- Added manifest, evaluated-project, `.nuspec`, assembly, packaged-consumer assets, artifact-manifest, reproducibility, and all-or-none NuGet publication gates.

### Core
- `omy.Utils` moves from 1.2.1 to the coordinated major candidate; see the API audit and migration guide.

### IO and serialization
- `omy.Utils.IO` and `omy.Utils.IO.Serialization.Generators` move from 1.2.1 and are validated as a runtime package and analyzer respectively.

### Networking
- `omy.Utils.Net` moves from 1.2.1 and retains its `net9.0` target.

### Data
- `omy.Utils.Data` moves from 1.2.1 and its parser/generator build graph is validated explicitly.

### Imaging and fonts
- `omy.Utils.Imaging` and `omy.Utils.Fonts` move from 1.2.1 with their transitive product dependencies synchronized.

### Geography
- `omy.Utils.Geography` moves from 1.2.1.

### Mathematics
- `omy.Utils.Mathematics` moves from 1.2.1; existing breaking mathematics changes remain documented below. `omy.Utils.Collections` is not part of this release candidate: it is an independent, provisional NuGet package at `0.0.1`, packaged and published separately - see [provisional versioning](docs/releasing/ProvisionalVersioning.md).

### OData
- `omy.Utils.OData` moves from its published 0.0.1 and `omy.Utils.OData.Generators` moves from 0.0.1.

### Dependency injection
- Runtime and generator packages move from 1.2.1 and are treated as a synchronized runtime/analyzer pair.

### Virtual machine
- `omy.Utils.VirtualMachine` moves from 0.1.0.

### Number formatting
- `omy.Utils.NumberToString` establishes its first public baseline; the ordinal/year additions remain itemized below.

### Parser
- The six parser packages establish their first release-candidate API baselines without expanding the production support contract.

### Source generators
- All four generator packages are packaged under `analyzers/dotnet/cs`, restored by real consumers, and checked for Roslyn load errors.


### Changed — `omy.Utils.Mathematics` (BREAKING)
- **`Matrix<T>.DiagonalizeLU()` now returns a 3-tuple `(L, U, P)` instead of `(L, U)`.** The previous
  two-factor result was mathematically unable to reconstruct the original matrix whenever partial
  pivoting swapped rows (the documented `A = L·U` identity only held for inputs that happened not to
  need a pivot swap; the fix also corrected `L` itself, which previously held the product of the
  elimination operators rather than the actual multiplier matrix). The new contract is `P * A = L * U`.
  Existing call sites using a two-element deconstruction (`var (l, u) = matrix.DiagonalizeLU();`) will
  fail to compile and must be updated to `var (l, u, p) = matrix.DiagonalizeLU();`. No package version
  bump yet — tracked for the coordinated `omy.Utils.*` 2.0.0 batch release rather than an individual
  bump.
- `Solve`/`Invert` singularity checks now use a scale-aware relative pivot tolerance (derived from the
  scalar type's own machine epsilon) instead of comparing to exact zero; both gained an optional
  `relativeSingularityTolerance` parameter to override the default. A previously-accepted matrix whose
  elimination pivot is merely close to zero (relative to the matrix's magnitude) now throws
  `InvalidOperationException` instead of returning a huge/NaN result.

### Changed — `omy.Utils.Reflection` (BREAKING, v1.2.1 → 2.0.0)
- **`LibraryMapper.Emit<TInterface>` now runs in an isolated, sandboxed worker process by default**,
  instead of compiling and loading the generated mapping class directly in the calling process. Same
  method signature, different runtime behavior and requirements:
  - Host applications must call `LibraryMapper.RunWorkerIfRequested(args)` as the very first statement
    of their entry point, before any other startup logic, or `Emit<TInterface>` fails to start the worker.
  - Only interfaces whose members use JSON-representable types (primitives, `string`, enums, and
    arrays/structs made of these) can be mapped this way; `Emit<TInterface>` now throws
    `NotSupportedException` immediately for interfaces using `IntPtr`/pointers/handles or arbitrary
    reference types, which previously worked (in-process, without isolation).
  - Every call now round-trips over a named pipe (JSON serialization both ways) instead of a direct
    in-process delegate call, with a real performance cost per call.
  - The original unsandboxed behavior is preserved as `LibraryMapper.EmitInProcess<TInterface>` (and
    `EmitDllMappableClass.Emit`), gated behind `[Experimental("UTILSREFL001")]` — callers must
    explicitly acknowledge the code-injection risk documented on that method to keep using it.
- `ProcessContainerPermissions.Default` is now a fresh, immutable instance per access instead of a
  shared mutable singleton; all properties are `init`-only.

### Added — `omy.Utils.Reflection`
- `LibraryMapper.Emit<TInterface>` gains optional `loadTimeout`/`callTimeout` parameters; the isolated
  worker's Load/Call/Shutdown requests are now bounded (30s/30s/5s by default) instead of blocking
  indefinitely on a hung native call.
- `EmitWorkerPool`: opt-in sharing of a single isolated worker process across several mapped interfaces,
  trading some isolation between them for a lower per-interface process-spawn cost.
  `LibraryMapper.Emit<TInterface>` itself is unchanged (still one worker per interface by default).
- `ProcessIsolation` hardening: sandboxed child environment allowlisting (`SandboxedProcessEnvironment`,
  now applied to the Windows AppContainer worker as well as Linux/macOS), `AppContainerSandbox` Job
  Object failure handling, `PATHEXT`-aware `CommandAvailability.Exists`.
- `EmitDllMappableClass`/`LibraryMapper.Emit` reject generic interfaces and generic methods upfront with
  a clear error, instead of failing later with a cryptic Roslyn diagnostic.
- `Platform.IsMacOS` alias for `Platform.IsMacOsX`.

### Added — `omy.Utils.NumberToString`
- **Ordinaux EL** : Grec — word rules pour masculin (défaut) + `OrdinalVariants` gender=θηλυκό et gender=ουδέτερο pour 1-12, dizaines et centaines.
- **Ordinaux FI** : Finnois — word rules exhaustives pour toutes les formes (unités 1-9, exceptions 11-19, dizaines 20-90, centaines, туhat).
- **Ordinaux HI** : Hindi — suffixe `वाँ` pour 5-9 et 11+ ; exceptions explicites pour 1-4 et word rule pour 6 (छठा).
- **Ordinaux PL** : Polonais — word rules pour toutes les formes nominatif masculin singulier (unités, 11-19, dizaines, centaines, tysiąc). Pour les ordinaux composés, seul le dernier mot est transformé (limitation XML).
- **Ordinaux AR** : Arabe — exceptions masculines indéfinies pour 1-10.
- **Ordinaux WO** : Wolof — suffixe `ël` + exception 1 (`bu njëkk`).
- **FR ordinal féminin** : `ConvertOrdinal(1, "gender=feminin")` retourne "première" pour les cultures `FR-fr-ca` et `FR-be-ch` via `<OrdinalVariants>`.
- **Ordinaux RU** : nouvelles règles d'ordinaux en russe — suffixe "ый" avec `removeTrailing="ь"`, word rules pour toutes les formes irrégulières (unités, dizaines, centaines, тысяча).
- **`ConvertYear(int year)`** : nouvelle méthode pour lire une année en mots ; pour les langues configurées avec `<YearFormat>` et `<SplitRange>`, l'année est découpée en deux moitiés (ex. EN : 1984 → "nineteen eighty-four", 1900 → "nineteen hundred", 1905 → "nineteen oh five").
- **Ordinal variants**: `ConvertOrdinal(int, params string[])` — ordinals can now be inflected for gender and other dimensions using the same `"dimension=value"` syntax as `Convert`. Languages with variant ordinals: ES, IT, PT, CA, GL, HE.
- **Prefix ordinals**: `<Ordinals prefix="…">` in the XML configuration produces ordinals by prepending a fixed string to the cardinal. Used by ZH (第), JA (第), KO (제), EE (etsõ).
- **`IOrdinalLanguageSpecifics`**: new interface that can be implemented alongside `INumberToStringLanguageSpecifics` to override ordinal formation with custom logic (highest priority, falls back to XML pipeline when `TryConvertOrdinal` returns `false`).
- **`SupportsOrdinals` property** on `INumberToStringConverter`: returns `true` when the converter has any ordinal configuration (exceptions, word rules, suffix, or prefix).
- **Ordinal support** extended to: DE (German), ES (Spanish), IT (Italian), PT (Portuguese), CA (Catalan), GL (Galician), HE (Hebrew), EE (Ewe), ZH (Chinese), JA (Japanese), KO (Korean).
- **Swiss/Liechtenstein German config** (`de-CH`, `de-LI`): separate configuration without the `"ein tausend" → "tausend"` contraction used in standard German. Ordinals follow the same rules as DE with an explicit exception for 1000 → "tausendste".

### Changed — `omy.Utils.NumberToString`
- Updated `Utils.NumberToString/README.md`: corrected ordinal support matrix, added ordinal examples for DE, ES, IT, PT, CA, GL, HE, EE/ZH/JA/KO, documented `IOrdinalLanguageSpecifics`, `SupportsOrdinals`, prefix ordinals, and `<OrdinalVariants>` XML syntax.

### Changed
- Added generated C# opt-in allocation of declared parser rule locals as missing-only untyped `null` invocation-frame entries before `@init`, while preserving conservative `Parse(...)` behavior.
- Clarified parser source-coordinate documentation across `omy.Utils.Parser.Source` and the parser roadmap, including the split between runtime offsets and human-readable diagnostic/tooling locations.
- Updated the `omy.Utils` NuGet description to a concise consumer-facing summary aligned with the package README and discoverability goals.

### Added
- Added `omy.Utils.Parser.Source` as a shared source-location contracts package for `SourceCodeLocation` and `SourceCodeRange` without requiring a diagnostics dependency.
- Clarified parser runtime documentation for policy-controlled semantic predicates/actions, conservative defaults, memoization assumptions, and related diagnostics semantics.
- Corrected package casing reference from `omy.Utils.Xml` to `omy.Utils.XML` in the base package README to match the published NuGet package identifier.
- Refined consumer documentation: updated root README and getting-started guide with complete package inventory, install-first flow, and explicit consumer vs contributor requirements.
- Clarified getting-started and release documentation with csproj-derived TFM guidance, source-generator install example, and explicit CI workflow mapping.
- Added `omy.Utils.Parser` (v0.1.0): self-describing universal parser framework. Tokenizes and parses any ANTLR4 grammar at runtime without code generation. Includes `LexerEngine`, `ParserEngine`, `Antlr4GrammarConverter`, and `RuleResolver`.
- Added XML documentation (English) to all `Utils.Parser` public and private members.
- Added `PackageTags`, `PackageReadmeFile`, `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to `omy.Utils.Parser.csproj`.
- Added `RepositoryUrl`, `RepositoryType`, and `PackageProjectUrl` to all other packable project files.
- Added consumer-focused documentation, getting started guide, GitHub About proposal, and release process notes.
- Marked internal projects (`Utils.Expressions.CSyntax`, `Utils.Parser.VisualStudio.Worker`) as non-packable to keep NuGet metadata scope limited to published packages.
- Documented package family overview and usage in the root README and base package README.

### Packaged product-train acceptance

- Centralized the `omy.Utils` and parser-train candidate versions, made Antlr4.Common packable, embedded the DateFormula runtime configuration, and added manifest-driven package-only acceptance validation.

### Utils.IO 2.0 breaking changes

- Migrated `ReaderWriterGenerator` from `ISourceGenerator` to `IIncrementalGenerator` and made generated method identities collision-free.
- Removed the declared 1.x stream/helper signatures recorded in `eng/api-breaking-changes/2.0.0.json`; no compatibility shims are provided.
- Reader converters now require an exact return type, while base/interface writer converters use deterministic most-specific selection.
- `PartialStream` now provides synchronized position, bounds, Span, and asynchronous semantics.

### Number-to-string deterministic rules

- **Breaking:** `VariantRule` and `OrdinalVariantRule` now expose immutable signed `Priority` values; trigger tuples were replaced by `TriggerReplacementForm`.
- Canonical constraint sets, shared specificity/priority ranking, and construction-time `UNTS001`–`UNTS004` diagnostics reject unresolved intersections instead of using declaration order.
- XML `priority` attributes default to zero and cumulative variants apply in ascending specificity and priority order.

### Changed — `omy.Utils.Net` (BREAKING)
- Added exclusive protocol exchanges, fail-closed poisoned sessions, structured response exceptions, and bounded streaming POP3/NNTP payload APIs.
- Added strict SMTP paths/options, strict UTF-8 SASL, exclusive mail transactions, and bounded verified RSET recovery.
- POP3 and NNTP mandatory numeric responses are strict; NEWNEWS and `INntpArticleStore.ListNewsSinceAsync` use message IDs, and NEXT returns null only for 421.
