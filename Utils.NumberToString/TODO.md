# Utils.NumberToString — Current backlog

Re-audited on 2026-10-16 after closing NTS-40 and NTS-41 together (one declined-cardinal builder per language,
internal to the fraction plugins: the oblique cases `двом третім`, `трьома п'ятими`, `dvěma třetinám`, `třemi sty
šedesáti pěti tisícinami`, `dvom tretinám`, `dvadsaťjeden stotinám`; the slash readings `jedna lomeno dvaceti jednou`,
`jedna lomeno dvěma tisíci dvaceti třemi`, `jedna lomené dvoma miliónmi`, `jedna lomené dva milióny troma`; NTS-42
opened for the remaining closures), after closing NTS-39 (the UK, BG, CS, SK fraction domains NTS-30H left closed: compound
numerators by each language's agreement rule, `двадцять одна тридцять сьома`, `сто и една хилядни`, `dvacet dva
třetin`, `dvadsaťdva tretín`; every IJP fraction noun and the complete Slovak paradigms; the mathematical readings
`jedna lomeno dvaceti třemi`, `jedna lomené troma`, `jedna lomené sto štyridsiatimi dvoma`, `jedna lomené číslom dvesto`; `nula tretín`; denominator one and
zero in UK/BG deliberately closed; NTS-40 and NTS-41 opened), on 2026-10-15 after closing NTS-30H (UK, BG, CS, SK fractions read as these languages read them, through
the generic `IFractionLanguageSpecifics`: `одна третя`, `дві третіх`, `една втора`, `седем осми`, `jedna třetina`,
`pět sedmin`, `dve tretiny`; the connector reading `один на тисяча` / `jedna ku tisíc` is gone, unsourced values
fail closed; NTS-39 opened), after closing NTS-30G (Basque *eta* only before the last group of a numeral phrase,
`hirurogei milioi berrehun mila eta bat`, through the generic `finalGroupConnector`; Spanish *millón/millones* force a
masculine multiplier, `quinientos millones` under `gender=femenino`, through `forceVariants` on the forms of an exact
`<ScaleForm>`), after closing NTS-30F (Spanish and Basque milliards read as a thousand millions through
`pairedScalesFrom="2"`: `dos mil quinientos millones`, `mil un millones`, `mila eta bostehun milioi`; NTS-38 and two
NTS-30 items opened), after closing NTS-37 (Finnish long scale without *-ljardi* above *miljardi*: unbounded
cardinals `miljoona`, `kaksi miljoonaa`, `tuhat biljoonaa`, `kvadriljoona`, through the generic
`pairedScalesFrom` primitive; ordinals to 1 999 999, `kolmastuhannes seitsemässadaskuudeskymmenes`, `miljoonas`;
`nollaa`, `nollan`), on 2026-10-14 after closing NTS-30E (Finnish cardinals: one word inside a group, multiplier and
*tuhat/tuhatta* joined, one space between groups: `kaksikymmentäyksi`, `kaksituhatta`,
`kaksikymmentäyksituhatta satakaksikymmentäyksi`; every component inflected in the partitive and genitive:
`kahtakymmentäyhtä`, `kahdenkymmenenyhden`; generic `multiplierSeparator` on `NumberScale` and `<ScaleForm>`;
NTS-37 opened), after closing NTS-30D (Bulgarian *и* before the last component of every numeral phrase,
a multiplier included: `двадесет и едно`, `сто двадесет и едно`, `хиляда и сто`, `двадесет и една хиляди`, `двадесет и
един милиона`, through the generic `finalComponentConnector` primitive), after closing NTS-30C (the generated Conway scale nouns of RU, UK, BG, CS and SK agree
with their multiplier through one `<GeneratedScaleForm>` per suffix family: `два триллиона`, `два трильйони`,
`два трилиона`, `dva biliony`, `dvě biliardy`, `päť biliárd`, up to grouped names such as `миллиниллион`;
NTS-35 and NTS-36 opened), after closing NTS-30B (UK, BG, CS, SK thousands, millions and milliards agree with
their multiplier: `дві тисячі`, `два мільйони`, `две хиляди`, `два милиона`, `dva tisíce`, `pět milionů`,
`dvě miliardy`, `dva milióny`, `päť miliárd`; Slovak cardinals in the PSP one-word spelling `dvadsaťdva`, `päťsto`,
`dvetisíc`; `UkrainianPluralLexicalFormSelector`, `CzechSlovakPluralLexicalFormSelector`; NTS-30C broadened,
NTS-30D opened), on 2026-10-13 after closing NTS-30A (decimal reading modes `LanguageDefault`/`DigitByDigit`/
`MathematicalFraction`; decimal zeros read with the language's `zero`: `21.05` → `eenentwintig komma nul vijf`,
`二十一点零五`; `decimalDigitByDigitMaxDigits` and `mixedFractionSeparator`; sourced French denominators to the
`cent-milliardième` and English to the `hundred-thousandth`: `un dix-millième`, `deux et un dix-millième`; NTS-33
and NTS-34 opened), after
closing NTS-31 (Dutch ordinals from a million: `miljoenste`, `twee miljoenste`,
`twee miljoen tweehonderdduizendste` after the e-ANS and the Besluit bekendmaking spellingvoorschriften 2005, rule
6.N; NL ordinals over `-long.MaxValue`..`long.MaxValue`, `long.MinValue` being rejected by the engine), on
2026-10-11 after closing the cardinal part of NTS-31 (Dutch long
scale on `SCALE-LONG`: `miljoen`, `miljard`, `biljoen` … with `-joen`/`-jard` and the `l` junction, Conway tables
inherited, unbounded cardinals), after closing NTS-29 (attested ordinals of zero: DA `nulte`, HU `nulladik`) and NTS-32
(Polish ordinal of 400: `czterechsetny`), after closing NTS-28 (compound and large-scale ordinals in ES, PT, GL, EL, FI, PL, RU, UK; guards
widened for JA, KO, ZH, VN, HI, EU; SV, AR deliberately fail-closed; NL blocked by the new NTS-31; NTS-32 opened),
on 2026-10-10 after closing NTS-27 (cardinal defects found by NTS-08: SV, PL, RU, VN, JA/KO/ZH, HI,
EU, ES; generic `groupPattern` and `innerString`/`innerBuildString` primitives; opened NTS-30), on 2026-10-09 after closing NTS-08 (final linguistic validation of the ordinals and ClockTime;
opened NTS-27, NTS-28 and NTS-29), after closing NTS-21 (Wolof large scales: attested `milyoŋ`/`milyaar`, long-scale
Conway-Wechsler extension in Wolof spelling, unbounded cardinals; `suffixSeparator` primitive) and NTS-09
(`Trigger` inheritance through `baseOn`), on 2026-10-08 after closing NTS-25 (NTS-25B: ID/MS, SW and TR Conway tables localized to their
alphabets; NTS-25A: Cyrillic Conway tables, RU/BG/UK short scale, SW/TR junctions), and after closing NTS-24 (strict Conway-Guy-Wechsler scale names shared by every
SCALE-SHORT and SCALE-LONG language; opened NTS-25), on 2026-10-07 after closing NTS-23 (Ewe large scales:
Conway-Wechsler short scale inherited from SCALE-SHORT, unbounded domain; opened NTS-24), after closing NTS-22 (Ewe millions: static `miliɔn`,
domain 0–999 999 999; opened NTS-23), after closing NTS-20 (Ewe cardinals rebuilt to 999 999 and `-lia` ordinals
restored, with the generic `<Groups onScale>` and `multiplierPosition` primitives; opened NTS-22), after
closing NTS-19 (Wolof cardinals and ordinals to 999 999; opened NTS-21),
after closing NTS-16 (Wolof ordinals corrected, Ewe ordinals withdrawn; opened NTS-19 and NTS-20)
and after closing NTS-15 and NTS-18 (Italian ordinals complete: every
category is either supported on sourced evidence or a deliberate fail-closed limitation); on 2026-10-06 after closing NTS-17 and narrowing NTS-15 (which opened NTS-18), after
closing NTS-14 (which opened NTS-16 and NTS-17) and NTS-13 (which opened NTS-15); on 2026-10-03 after closing
NTS-10, NTS-11 and NTS-12 (which opened NTS-13 and NTS-14);
previously re-audited on 2026-10-01 after the NTS-08 regional consolidation (#608), which also
opened NTS-09. Historical details remain in
the archived audit files; this file is the active source of truth.

See `docs/releasing/TodoAudit-2026-08-16.md` for the repository-wide
classification.

## Open items

- **NTS-30 — cardinal defects found by NTS-27 and NTS-28 outside their lists.** Recorded, not fixed (the ES/EU
  long-scale item was fixed by NTS-30F, `DONE-2026-10-15(1).md`; the EU *eta* and ES feminine million items by
  NTS-30G, `DONE-2026-10-15(2).md`; the UK, BG, CS, SK fractions by NTS-30H, `DONE-2026-10-15(3).md`):
  - VN: a whole zero class after a higher class is skipped (`một triệu không trăm linh một`), whereas the
    SGK Toán 4 solution keys read it `không nghìn` (*năm mươi triệu không nghìn một trăm linh hai*); needs a
    zero-group rendering and a normative source.
  - ES currency: `uno euro` for DPD *un euro* (apocope before a noun); the caller-defined currency wording does
    not force the attributive form.
- **NTS-42 — the UK, CS and SK fraction readings NTS-40/41 left closed.** Each one fails closed for a named missing
  piece of evidence (`DONE-2026-10-16(1).md`). Audited on 2026-10-19 (`DONE-2026-10-19(1).md`): every family reproduced
  as a deliberate `NotSupportedException` of the language plugin, none silently wrong, no engine defect (zero and the
  case reach the plugin unchanged); the first five items stay open on missing linguistic evidence only, the last one on
  a design dependency. Closures pinned by `NumberToStringNts42Tests`:
  - CS, oblique cases of a compound numerator ending in *jedna* (21/100, 101/3, 1001/3 in the genitive): the IJP gives
    only the nominative (*dvacet jeden žák* / *dvacet jedna žáků*); the gender and number the noun takes after a
    declined *dvaceti jedné*/*dvaceti jednomu* are not sourced.
  - CS, a multiplier ending in one after the tens or hundreds (21 000, 101 000, 31 000 000) in the *lomeno* reading and
    in the oblique cases: no source gives the gender of its *jedna* before *tisíc*/*milion* outside the nominative.
  - SK, oblique cases of a numerator ending in a bare *jeden* after a larger group (101/3, 1 000 001/3): Morfológia
    gives the indeclinable compounds with *-jeden* (*ku štyridsaťjeden družstvám*) but not *sto jeden*.
  - CS and SK, the *lomeno*/*lomené* readings in an oblique case: they are fixed mathematical phrases whose denominator
    is already an instrumental; no source declines the phrase as a whole.
  - UK, CS and SK, a zero numerator in an oblique case: Morfológia reads *nula* only in the direct cases (*nula
    stupňov*); no source gives *нуля*, *nuly*, *nule* + a fraction.
  - CS and SK denominators, and UK, CS and SK oblique numerators, from 10^12: the builders stop below *bilion*/*bilión*.
    Not a source gap: the names from 10^12 are generated by the engine's Conway machinery (`<GeneratedScaleForm>`,
    nominative count forms only), which the plugins cannot reach. Opening it needs a generic way for the engine to supply
    a generated scale noun in a case (design to specify first; no second spelling of the Conway names in a plugin), and
    NTS-36 for the CS/SK names from 10^24.
- **NTS-38 — ES and EU ordinals from the million, after NTS-30F.** Opened by NTS-30F, which left every ordinal
  byte-identical. EU: `ConvertOrdinal(1 000 000)` gives `milioi batgarren`, whereas Euskaltzaindia rule 203 §5
  writes *milioigarren* (and *milioi bat eta batgarren* for 1 000 001, *bi milioigarren* for 2 000 000); rule 203
  is newer than the rule 18 audited by NTS-08/NTS-28 and may settle more ordinals. ES and EU: the ordinals from
  10^9 stay closed by `SpanishOrdinalLanguageSpecifics` / `BasqueOrdinalLanguageSpecifics` although the cardinal is
  now composed (DPD "ordinales" attests *milmillonésimo*); opening them needs their own audit.
- **NTS-35 — Polish cardinals above 999 999.** Excluded from NTS-30C: PL declares `maxNumber="999999"`, so no
  generated scale is part of its public domain and NTS-30C configured none. Opening the domain needs, together: the
  Polish long-scale names (*milion*, *miliard*, *bilion*, *biliard* …, not the current generated spelling), their
  productive inflection after the multiplier (*dwa miliony*, *pięć milionów*, *dwadzieścia dwa miliony*: the
  `PolishPluralLexicalFormSelector` rule, through `<GeneratedScaleForm>` for the generated families), the multiplier
  grammar, and a source audit (WSJP PAN, SJP PWN).
- **NTS-36 — CS and SK generated scale names keep the Latin Conway prefixes.** Found by NTS-30C, not fixed: CS and
  SK inherit the SCALE-LONG Latin tables, so 10^24 is `quadrilion` / `quadrilión` and 10^30 `quintilion` /
  `quintilión`, whereas the IJP writes *kvadrilion* (ÚJČ AV ČR, entry *kvadrilion*) and the Slovak series is
  *kvadrilión*, *kvadriliarda*, *kvintiliarda* (Slovak Wikipedia "Biliarda"). The agreement of NTS-30C is right on
  these names (*dva quadriliony*); only the spelling of the prefix tables is at stake (*kv-* for *qu-*, and the
  other Latin components still to be checked), which needs its own audit as NTS-25 did for Cyrillic.
- **NTS-33 — French ordinals keep the plural `-s` of a multiplied scale noun.** Found by NTS-30A, not fixed:
  `ConvertOrdinal` gives `deux centsième` (200), `trois cents millionsième` (300 000 000), `deux millionsième`,
  `dix milliardsième`, `deux billionsième` for *deux centième*, *trois cent millionième*, *deux millionième*,
  *dix milliardième*, *deux billionième*: the ordinal suffix is appended to the plural cardinal form (`cents`,
  `millions`, `milliards`), and the plural `cents` before a scale noun is kept as well. The decimal fraction
  names (`dix-millième(s)`, `millionième(s)` …) are separate strings and are not affected.
- **NTS-34 — decimal reading audit of the other languages.** Opened by NTS-30A, which audited FR and EN only.
  Every other language reads its decimals digit by digit with no limit, no named fraction and no mixed-number
  connector, none of which is linguistically validated: per language, whether named decimal fractions exist
  (`<Fractions>`, then `MathematicalFraction`), the mixed-number connector (`mixedFractionSeparator`), whether the
  automatic digit-by-digit reading needs a limit (`decimalDigitByDigitMaxDigits`), and the word for a pronounced
  decimal 0 (the configured `zero`, e.g. EE `naneke o`). Also: HU writes the decimal digits without a separator
  (`egyegészkettőöt`); FR beyond 11 digits and EN beyond 5 have no sourced denominator (*billionième*,
  *millionth*); the FR numerators are written without hyphens (`vingt cinq millièmes`), which the Académie
  (2022) shows to be ambiguous with *vingt cinq-millièmes*.
- **NTS-26 — Wolof connective `-i` inside nested numeral multipliers.** Re-audited 2026-10-11:
  the evidence supports **real variation**, not a demonstrated defect in the current default. The current
  configuration writes the multiplier of `téeméer` with `-i` (`juróom ñenti téeméer` 900) and keeps that
  connective when the hundreds phrase is itself the multiplier of a higher scale
  (`juróom ñenti téeméeri milyoŋ`). This analysis is supported by Guérin (CNRS/LLACAN), who describes the
  multiples of hundreds and thousands as genitive constructions and gives nested forms such as
  `ñaar-i téeméer-i dërëm`; pedagogical sources also attest forms such as `juróom ñaari téeméer`,
  `ñenti téeméer` and `ñaari junni`. Conversely, the Wolof Ajami Reader (Ngom et al., Boston University,
  2025, p. 192) writes `juróom ñent téeméeri milyoŋ` and `juróom ñaar téeméeri milyoŋ`, i.e. without the
  inner `-i` before `téeméer`; Kosogorova 2023 and Gaye 1980 also record forms without that connective
  (Gaye treats `-i` as optional with `ñaar`). The decree 2005-992 establishes the general attached
  `-u/-i` genitive linker but does not settle these nested numeral chains. **Do not change the default output
  from NTS-19/NTS-21 on the present evidence.** Next step, if pursued: targeted corpus work on
  `... téeméeri milyoŋ/milyaar` and comparable nested hundreds, to determine whether the two patterns are
  dialectal/register variants, context-dependent, or freely optional. Only if that evidence justifies it,
  consider a variant dimension; avoid a silent default change because it would affect many byte-stable Wolof
  cardinals below and above a million.


NTS-01 through NTS-05, NTS-08, NTS-09, NTS-10 through NTS-25, NTS-27, NTS-28, NTS-29, NTS-30A, NTS-30B, NTS-30C, NTS-30D, NTS-30E, NTS-30F, NTS-30G, NTS-30H, NTS-31, NTS-32, NTS-37, NTS-39, NTS-40 and NTS-41 are closed:

- NTS-01 — XSD validation: `DONE-2026-08-21.md`.
- NTS-02 — initialization isolation: `DONE-2026-08-21.md`.
- NTS-03 — single composite finalization: `DONE-2026-08-21(1).md`.
- NTS-04 — constituent-local `ForcedVariants`: `DONE-2026-08-24(1).md`
  (supersedes the deferral recorded in `DONE-2026-08-21(1).md`).
- NTS-05 — extensible lexical form selection + Spanish attributive apocope:
  `DONE-2026-08-25(1).md` (resolves the Spanish deferral recorded in
  `DONE-2026-08-24(1).md`), with pre-merge review fixes (selector-specific
  XML configuration, per-type reflection activation caching, and the
  `TimeUnitForms`/`TimeUnitFormSelectors` effective-state correction) in
  `DONE-2026-08-25(2).md`, and a second review round (selector activation
  caching reuses `Utils.Collections.CachedLoader` instead of a bespoke
  cache) in `DONE-2026-08-25(3).md`.
- NTS-10 (IT/HI/AR cardinals), NTS-11 (HE thousands), NTS-12 (zero ordinal) and the `<Fusion>`
  primitive: `DONE-2026-10-03.md`.
- NTS-13 (Italian compound ordinals) and the `<OrdinalStem>` primitive: `DONE-2026-10-06.md`.
- NTS-14 (IT millions, AR thousands, HE compound ordinals, zero ordinals) with scale lexical forms
  and ordinal-only replacements: `DONE-2026-10-06(1).md`.
- NTS-17 (Italian round million-scale ordinals) with the `<OrdinalScale>` and `<OrdinalComposition>`
  primitives, and the attested part of NTS-15 (1010, 100001–100009): `DONE-2026-10-06(2).md`.
- NTS-15 (Italian non-round thousands, 1110–1910) and NTS-18 (Italian ordinals from a biliardo,
  non-round values from a million): round biliardo/trilione ordinals implemented, the unsourced
  families closed as deliberate fail-closed limitations: `DONE-2026-10-07.md`.
- NTS-16 (Wolof and Ewe ordinals): Wolof `-eel` on 1–999, Ewe ordinals withdrawn pending NTS-20:
  `DONE-2026-10-07(1).md`.
- NTS-19 (Wolof cardinals: orthography and thousands; ordinals from 1000, spelling `-éel`):
  `DONE-2026-10-07(2).md`.
- NTS-20 (Ewe cardinals rebuilt, `-lia` ordinals restored; generic `<Groups onScale>` and
  `multiplierPosition` primitives): `DONE-2026-10-07(3).md`.
- NTS-22 (Ewe millions: static `miliɔn`, domain 0–999 999 999; billions deferred to NTS-23):
  `DONE-2026-10-07(4).md`.
- NTS-23 (Ewe large scales: Conway-Wechsler short scale from SCALE-SHORT, `akpe`/`miliɔn`/`biliɔn`/
  `triliɔn` anchors, unbounded domain; `kpakple` not corroborated): `DONE-2026-10-07(5).md`.
- NTS-24 (strict Conway-Guy-Wechsler scale names for every SCALE-SHORT/SCALE-LONG language: `uni` → `un`,
  `vingti` → `viginti`, terminal `[a|-illi=>i]` endings, grouped names above the 999th restarting from
  `Scale0Prefixes`, single Conway linking consonant): `DONE-2026-10-08.md`.
- NTS-25 (localized Conway names): NTS-25A (Cyrillic Conway tables, RU/BG/UK short scale with a static
  milliard, SW and TR Conway junctions, multi-letter linking markers) in `DONE-2026-10-08(1).md`; NTS-25B
  (ID/MS, SW and TR Conway tables localized to their alphabets, ID/MS junctions) in `DONE-2026-10-08(2).md`.
- NTS-21 (Wolof large scales: attested `milyoŋ`/`milyaar`, productive long-scale Conway-Wechsler names in
  Wolof spelling, `-i` on the multipliers, unbounded cardinals, ordinals kept to 999 999; the
  `suffixSeparator` primitive): `DONE-2026-10-09.md`.
- NTS-09 (`Trigger` elements inherited through `baseOn`; an empty list is read as absent), closed with NTS-21,
  which introduced the first production trigger: `DONE-2026-10-09.md`.
- NTS-08 (linguistic validation of every ordinal and ClockTime capability: validated, corrected or fail-closed;
  `OrdinalDomainGuardLanguageSpecifics`; NTS-27, NTS-28 and NTS-29 opened): `DONE-2026-10-09(1).md`, after the
  implementation passes of `DONE-2026-10-01(1).md`.
- NTS-27 (cardinal defects found by NTS-08: SV one word and *en*, PL/RU agreement of the thousands, VN classes
  read in full, JA/KO/ZH counting by 万/만 with 零/一十 and 제44항 spacing, HI lakh/crore and पाँच, EU rule 7,
  ES apocope; generic `groupPattern` and `innerString`/`innerBuildString`, `RussianPluralLexicalFormSelector` and
  `PolishPluralLexicalFormSelector`; NTS-30 opened): `DONE-2026-10-10.md`.
- NTS-28 (compound and large-scale ordinals: ES, PT, GL, EL, FI composed by `<OrdinalComposition>`/ordinal
  replacements, PL, RU, UK round values in their plugins, JA/KO/ZH/VN/HI/EU guards widened to the cardinal domain,
  SV/AR deliberately fail-closed, NL blocked by NTS-31; guard extension point `TryBuildOrdinal`; NTS-31, NTS-32
  opened): `DONE-2026-10-11.md`.
- NTS-29 (attested ordinals of zero: DA *nulte* through a Danish-only hook of the Scandinavian base, HU
  *nulladik*; NO, SV and PL still fail closed) and NTS-32 (Polish ordinal of 400 *czterechsetny* in its 30
  forms, after WSJP PAN and SJP PWN): `DONE-2026-10-11(1).md`.
- NTS-31, cardinal part (Dutch long scale: `baseOn="SCALE-LONG"`, `maxNumber` removed, suffixes `joen`/`jard` with
  `suffixSeparator="l"`, Conway tables inherited with the Latin `quadri`/`quinti`/`sexti`/`octi`, `een` kept from
  `miljoen` upward, singular scale nouns, spaced groups without `en`; ordinals still guarded at 999 999):
  `DONE-2026-10-11(2).md`.
- NTS-31, ordinal part — **NTS-31 fully closed** (Dutch ordinals from a million over the full supported ordinal
  domain `-long.MaxValue`..`long.MaxValue`; `long.MinValue` is rejected by the engine). e-ANS
  (rangtelwoorden): the scale nouns take `-ste` (`miljoenste`, `miljardste`) and the ordinal keeps the spelling of
  the cardinal, ordinalizing only its final element. Besluit bekendmaking spellingvoorschriften 2005, rule 6.N:
  `de twee miljoenste`, `de twee miljoen tweehonderdduizendste`, `de twee miljoen tweehonderdduizend
  tweehonderdtweeëntwintigste`. Hence a round value with the multiplier one is the scale noun alone (`miljoenste`,
  not `een miljoenste`), a larger multiplier stays a separate cardinal word (`twee miljoenste`, not
  `tweemiljoenste`), the earlier groups of a compound stay cardinal (`een miljoen eerste`). Declarative only:
  `<OrdinalScale scales="2..6" multiplierSeparator=" " />`, stems `joen → joenst` and `jard → jardst`,
  `DutchOrdinalLanguageSpecifics` widened to 0–`long.MaxValue`: `DONE-2026-10-13.md`.
- NTS-30A (split out of NTS-30; its other items stay open): decimal zero digits no longer dropped (a pronounced
  decimal digit is the standalone `zero` or cardinal, not the level-1 composition text); `DecimalReadingMode`
  (`LanguageDefault`, `DigitByDigit`, `MathematicalFraction`) on `DecimalFormatOptions.ReadingMode`, a caller
  `DecimalSuffix` taking precedence; the language fallback limit `decimalDigitByDigitMaxDigits` (absent =
  unlimited, `0` = none, inherited through `baseOn`) and the mixed-number connector `mixedFractionSeparator`; French
  named fractions to 11 digits (`dix-millième` … `cent-milliardième`, OQLF, Académie) with `et` (Éduscol), English
  to 5 digits (`ten-thousandth`, `hundred-thousandth`) with `and` (OpenStax): `DONE-2026-10-13(1).md`.
- NTS-30B (split out of NTS-30): the scales 1–3 of UK, BG, CS and SK agree with their multiplier, by `<ScaleForm>`
  and declarative multiplier-gender replacements, with the new `UkrainianPluralLexicalFormSelector` (CLDR uk, shared
  with ru) and `CzechSlovakPluralLexicalFormSelector` (one = 1, few = 2–4, other; compounds take the genitive, IJP) and
  the default selector for the Bulgarian count form (*два милиона*); Slovak cardinals written after the PSP (*dvadsaťdva*,
  *päťsto*, *dvetisíc*, *dvadsaťsedemtisíc*, *sedemtisíc dvesto osemdesiattri*, Jarošová 2021); generated scales
  deferred to NTS-30C, Bulgarian *и* to NTS-30D: `DONE-2026-10-14.md`.
- NTS-30C (split out of NTS-30): the generated Conway scale nouns of RU, UK, BG, CS and SK agree with their
  multiplier, productively, through the new `<GeneratedScaleForm suffix="…">` primitive (one entry per
  `NumberScale` suffix family, suffix fragments joined to the Conway prefix, optional `forceVariants` on the
  multiplier of a form; `NumberToStringConverterOptions.GeneratedScaleForms`, `GeneratedScaleForm`): RU *два
  триллиона*, UK *два трильйони* (the `(ів)` suffix removed), BG *два трилиона*, CS *jeden bilion*, *dvě biliardy*,
  SK *päť biliónov*, *dve biliardy*, *päť biliárd*. CS compounds keep the counting form before *-iarda* (*dvacet dva
  biliard*, *dvacet dva miliard*), decided on the IJP (*dvacet jedna žáků*, *dvacet dva korunám*), which closes the
  NTS-30B Czech gender note. PL excluded (NTS-35); HR left on its scale-specific replacements; CS/SK prefix spelling
  opened as NTS-36: `DONE-2026-10-14(1).md`.
- NTS-30D (split out of NTS-30): Bulgarian compound cardinals take *и* before the last component of every numeral
  phrase, independently in each multiplier of a scale noun (Речник на българския език, *и*, sense 3; ФСлФ СУ ezik.bg:
  *един милиард двеста милиона шейсет и пет хиляди седемстотин осемдесет и пет*): inside a group before its last
  non-zero position (*сто и петнадесет*, *сто двадесет и едно*), between groups before a one-component last group
  (*хиляда и сто*, *двадесет и една хиляди и едно*). New generic `finalComponentConnector` (XML attribute,
  `NumberToStringConverterOptions.FinalComponentConnector`), rejected with `groupConnector`, `intraGroupConnector` or
  a `Fusion`; ordinals and clock byte-identical: `DONE-2026-10-14(2).md`.
- NTS-30E (split out of NTS-30): Finnish cardinals follow Kielitoimiston ohjepankki ("numeroiden ryhmittely",
  "peruslukujen taivuttaminen"): one word inside a group (*kaksikymmentäyksi*, *satakaksikymmentäyksi*), multiplier
  and *tuhat/tuhatta* written together (*kaksituhatta*, *kaksikymmentäyksituhatta*), one space between groups;
  partitive and genitive inflect every component (*kahtakymmentäyhtä*, *sadankahdenkymmenenviiden*,
  *kolmeatuhatta*). New generic junction between a multiplier and its scale noun: `NumberScale multiplierSeparator`
  (`NumberScale.MultiplierSeparator`), overridden per exact scale by `<ScaleForm multiplierSeparator>`
  (`NumberToStringConverterOptions.ScaleFormMultiplierSeparators`); absent, the language separator applies, so every
  other language is byte-identical. The built-in bases state it explicitly: `" "` on the `SCALE-*` bases, `""` on HU
  (which inherits `SCALE-LONG` but writes `kétezer`). Ordinals and clock
  byte-identical; domain unchanged; NTS-37 opened: `DONE-2026-10-14(3).md`.
- NTS-37 (opened by NTS-30E): Finnish cardinals unbounded on the long scale (Kielitoimiston sanakirja: *miljoona*,
  *miljardi*, *biljoona*, *triljoona*, *kvadriljoona*, *kvintiljoona*; no *biljardi*, 10^15 *tuhat biljoonaa*),
  Conway tables in Finnish spelling, scale noun apart and in the partitive after any multiplier but the elided one
  (*miljoona*, *kaksi miljoonaa*, *kaksituhatta kolme biljoonaa*), every case through the scale fragments; new generic
  `NumberScale pairedScalesFrom` (`NumberScale.PairedScalesFrom`): from an index on, only every other scale is named
  and its multiplier spans two groups. Ordinals in the nominative to 1 999 999 with one space between groups
  (Kielitoimiston ohjepankki: *kolmastuhannes seitsemässadaskuudeskymmenes*, *miljoonas*; 1001–1999 no longer
  soldered); 2 000 000 and above, *miljardi* and the inflected ordinals fail closed (unsourced). *nollaa*, *nollan*.
  Dump FI-only: `DONE-2026-10-15.md`.
- NTS-30F (ES/EU item of NTS-30): Spanish and Basque read the milliard as a thousand millions, the multiplier of
  *millón*/*milioi* being one cardinal of up to six digits (DPD "números" *13 000 millones*, "millardo" *dos mil
  millones*; Euskaltzaindia rule 203: long scale, *berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi*),
  through the NTS-37 primitive `pairedScalesFrom="2"` with no engine change: *dos mil quinientos millones*, *mil un
  millones*, *veintiún mil un millones*, *mila eta bostehun milioi*, *mila eta bat milioi*. The scale-3 literals
  *mil millones*/*mila milioi* and their rules removed; domain, ordinals and clock unchanged; NTS-38 and two NTS-30
  items opened: `DONE-2026-10-15(1).md`.
- NTS-30G (the two NTS-30 items opened by NTS-30F): Basque writes one *eta* before the last group of each numeral
  phrase (Euskaltzaindia rule 203 §4: *hirurogei milioi berrehun mila eta bat*, *bi milioi eta zazpiehun mila*),
  through the new generic `finalGroupConnector` (inter-group assembly only; rejected with `finalComponentConnector`),
  replacing `groupConnector="eta"`; the stray `bat ehun → ehun` replacement removed (*milioi bat ehun mila eta bat*).
  Spanish *millón/millones* force a masculine multiplier (DLE *millón* m.; DPD "cardinales" §3, §6: the hundreds
  agree with the noun they quantify): *quinientos millones*, *dos mil quinientos millones* under `gender=femenino`,
  lower groups still feminine (*quinientos millones quinientas mil*), through the new `forceVariants` on the forms of
  an exact `<ScaleForm>` (`NumberToStringConverterOptions.ScaleFormMultiplierForcedVariants`, the
  `GeneratedScaleForm` contract, shields allocated for exact forms too). Dump ES (feminine only) and EU only; ES
  masculine, ordinals and clocks byte-identical: `DONE-2026-10-15(2).md`.
- NTS-30H (the NTS-30 item "UK, BG, CS, SK `ConvertFraction`"): the generic `IFractionLanguageSpecifics` extension
  point; UK and BG ordinal denominators (*одна третя*, *дві третіх*, *една втора*), CS and SK fraction nouns (*jedna
  třetina*, *pět sedmin*); unsourced values fail closed, NTS-39 opened: `DONE-2026-10-15(3).md`.
- NTS-39 (opened by NTS-30H): each closed item audited separately, inside the four plugins with no engine or API
  change. Compound numerators: UK singular after *одна* (*двадцять одна тридцять сьома*), BG plural (ИБЕ-БАН; *сто и
  една хилядни*), CS and SK genitive plural with the counting/base form (IJP *dvacet dva žáků*; Jarošová 2021
  *dvadsaťdva žien*). CS: every IJP fraction noun (13–20, round tens, 100 000, 10^9) and *lomeno* + instrumental for
  2–999 (SSJČ *dvě lomeno třemi*). SK: complete paradigms (KSSJ, Morfológia 1966 rule) and *lomené* + instrumental of the last
  component (*sto štyridsiatimi dvoma*, Slovenská reč 1978) or *lomené číslom* + cardinal when it is indeclinable
  (Ondrus 1968). Zero: SK *nula tretín*, CS *nula lomeno třemi*, UK/BG closed (no source).
  Denominator one closed everywhere (no source); oblique cases moved to NTS-40, the CS/SK slash readings beyond their
  domain to NTS-41. Dump: only fraction probes of UK, BG, CS, SK changed, all `NotSupportedException` → reading:
  `DONE-2026-10-16.md`.
- NTS-40 and NTS-41 (opened by NTS-39), implemented together: one internal declined-cardinal builder per language,
  shared by the oblique numerators (NTS-40) and the slash denominators (NTS-41); `Convert` keeps no case. UK: every
  part declined (Правопис 2019 §105–107: *двом третім*, *трьома п'ятими*). CS: every part declined but *sto* before
  another numeral (IJP; Prandstetter, Naše řeč 1938: *lomeno třemi sty padesáti šesti*), nouns after their IJP
  paradigms, final *jedna* → *jednou*. SK: last component declined (Ondrus §8), *milión*/*miliarda* declinable
  (Morfológia 329–330). Slash readings stay nominative-only; NTS-42 opened. Dump: only fraction probes of UK, CS, SK
  changed, all `NotSupportedException` → reading; BG byte-identical: `DONE-2026-10-16(1).md`.

Full multi-form plural systems (Russian/Slavic count-dependent noun forms,
Arabic dual/paucal/plural categories) were long deliberately out of scope — the
`ILexicalFormSelector` architecture supports them without redesign. Arabic (NTS-14),
Russian/Polish scale nouns (NTS-27) and the Ukrainian, Bulgarian, Czech and Slovak scales
1–3 (NTS-30B), and every generated scale of these five languages (NTS-30C), now use them. See the "Deliberately deferred" section of `DONE-2026-08-25(1).md` for the
original design decision.

New findings should be appended here as they are identified, and archived to
a dated `DONE-*.md` file once resolved, per the repository's `AGENTS.md`
TODO/DONE convention.


### NTS-08 final validation (2026-10-09, closed)

See `DONE-2026-10-09(1).md`.

### NTS-08 completion pass (2026-10-01)

See `DONE-2026-10-01(1).md`.

### NTS-08 completed slice (2026-09-23)

- Split Belgian and Swiss French into `baseOn="FR"` children; Belgian keeps `quatre-vingts` while Swiss keeps `huitante`.
- Split Valencian into a `baseOn="CA"` child with its own clock convention.
- Split Malay into a `baseOn="ID"` child, preserving the Malay `lapan` stem and a distinct `pukul` clock convention.
- Added productive Indonesian/Malay ordinal plugins handling suppletive `pertama` and `ke-` formation over language-specific cardinal stems.
- Added English and Catalan ClockTime rules. The remaining languages were completed on 2026-10-01 and validated on 2026-10-09 (see above).
