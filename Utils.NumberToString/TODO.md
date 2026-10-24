# Utils.NumberToString — Current backlog

Re-audited on 2026-10-24 after the final backlog pass, **which leaves no open NTS item**: NTS-54 (closed: the
Romanian scale multiplier agrees with its noun, feminine before *mii*, `douăsprezece mii`, `douăzeci și două de mii`,
`douăzeci și una de mii`, neuter before *milioane*/*miliarde*, `douăzeci și două de milioane`, `douăzeci și unu de
milioane`; DOOM 3, DOOM 2 §6.4, corpus sample and user decision; `<ScaleForm forceVariants>` and the new `gen=neutru`
value), NTS-35 (closed: Polish long-scale cardinals without bound, `jeden milion`, `dwa miliony`, `pięć miliardów`,
`kwadrylion`; SJP PWN, WSO PWN; `<GeneratedScaleForm>` with `PolishPluralLexicalFormSelector`; ordinals kept to
999 999) and NTS-34 (closed: decimal reading of every language audited; A DE, DA, NO, ES, ZH unchanged, B FR, EN
unchanged, the C and D languages fail closed under `LanguageDefault` with `decimalDigitByDigitMaxDigits="0"`, the
caller's `DigitByDigit` unchanged; `docs/NTS-34-decimal-reading-audit.md`); `DONE-2026-10-24.md`. Previously re-audited
on 2026-10-23 after NTS-53 (closed: Catalan plural *milions* and 10^9 *mil milions* (IEC Ortografia
§4.4.2.2a, DIEC2 "miliard"), Galician *mil millóns* (RAG "eón"), both through the ES/EU `pairedScalesFrom="2"`, Catalan
ordinals *milionè*, *mil milionè* (GBU §18.5, GIEC §17.2.5; user decision) and the masculine multiplier of *milió* /
*millón* under a feminine (GEIEC §12.3.2, NTS-55 closed with it); Romanian scale connector refined by
the generic `<ScaleConnector>` (no *de* after a multiplier ending on 1–19, GALR: *o sută nouăsprezece mii*, the public
`ScaleConnector`/`ScaleConnectorThreshold` kept at `de`/20) and currency *un euro*, *o liră* (DEX "unu") through
`<Currency><Replacement scope="Standalone">`; no multi-group replacement; NTS-54 opened;
`DONE-2026-10-23(3).md`), after NTS-52 (closed: one declarative connector between a currency numeral and its noun,
`<Currency><NounConnector>` with a default and substitutions on the terminal scale, the count and the beginning of the
noun, the most specific winning and the first declared at equal specificity; French `un-million d'euros`, `un-million
de francs`, Catalan `un milió d'euros`, `un milió de iens`, Galician `un millón de euros`, Romanian `douăzeci de euro`
but `o sută unu euro` after GALR; Portuguese audited, out of domain; ES/IT kept on the NTS-51 shorthand, unchanged; NTS-53 opened;
`DONE-2026-10-23(2).md`), after NTS-50 (closed: Basque `zerogarren`, Euskaltzaindia rule 203 §5, by the general
rule; only the domain guard changed) and NTS-51 (closed: Spanish `un millón de euros`, `dos mil quinientos millones de
euros` after DPD `cardinales` §6, through the generic structural `<Currency terminalScaleConnector>`; Italian `un
euro`, `uno scellino`, `ventuno euro` after Treccani, through the generic `<Currency><Replacement exceptBeforeNoun>`,
and `un milione di euro` through the same `terminalScaleConnector`;
NTS-52 opened; `DONE-2026-10-23(1).md`), after NTS-30 (closed: Vietnamese whole zero classes read `không` + class name, `năm mươi triệu
không nghìn một trăm linh hai`, through the generic `internalZeroGroupString`; Spanish currency numerals attributive, `un
euro`, `veintiún euros`, through the generic `<Currency forceVariants>`) and NTS-38 (closed: Basque `milioigarren`,
`mila milioigarren` after Euskaltzaindia rule 203 §5; Spanish `milmillonésimo` after DPD `ordinales` §2d; NTS-50 and
NTS-51 opened; `DONE-2026-10-23.md`), on 2026-10-22 after NTS-33 (French ordinals derived from the base of their last constituent: `deux
centième`, `deux millionième`, `trois cent millionième`, the fractions keeping `deux centièmes`; generic
`ordinalBaseString`), NTS-48 (French *vingt*/*cent* agreement: `quatre-vingts`, `deux cent mille`, `deux cents
millions`, through `<Groups onScale="1">`), NTS-49 (French numerals in the 1990 rectified spelling, every element hyphenated:
`vingt-et-un`, `quatre-vingt-un`, `soixante-et-onze`, `deux-cent-mille`, `deux-millions-un`) and the documentary closure of NTS-26 (Wolof `ñaari téeméeri milyoŋ` retained as
attested variation; `docs/NTS-26-wolof-nested-linker-choice.md`; `DONE-2026-10-22.md`), on 2026-10-21 after NTS-47 (NTS-42 closed: the SK genitive singular of a generated *-ón* noun opened,
`jedného trilióna tretín`, `jedného kvadrilióna tretín`, after the PSP *bilión, trilión, kvadrilión, kvintilión -a*; D
and E/CS closed as deliberately unsupported, `docs/NTS-42-deliberate-closures.md`; `DONE-2026-10-21(2).md`), after NTS-46
(NTS-42 E/UK and F/UK closed: the Ukrainian zero numerator declined as a noun
governing the genitive plural, `нулем третіх`, `нулеві третіх`, and the oblique numerators from 10^12 declining the
generated scale nouns read from the converter, `одним трильйоном третіх`, `двом квадрильйонам третіх`;
`DONE-2026-10-21(1).md`), after NTS-45 (NTS-42 families A, B and E/SK closed: Czech compounds and multipliers ending
in *jedna* declined in the oblique cases and in *lomeno*, `dvaceti jedné setiny`, `dvaceti jednomu tisíci třetin`,
`jedna lomeno dvaceti jedním tisícem`; Slovak zero with a fraction noun in every case, `nula tretinám`; D and E/CS
re-audited and kept closed; `DONE-2026-10-21.md`), on 2026-10-20 after closing NTS-36 (Czech and Slovak Conway names in their own spelling: `kvadrilion`,
`oktilion`, `kvatuordecilion`, `kvadrilión`, `kvintiliarda`) and NTS-42 family F for CS and SK (fractions from 10^12
declining the generated scale nouns read from the converter: `jednomu bilionu třetin`, `jedna lomeno pěti biliardami`,
`dvoma biliónmi tretín`; the UK part stays open; `DONE-2026-10-20.md`), on 2026-10-19 after closing NTS-44 (Slovak oblique fractions whose numerator ends in a bare *jeden*, read
as the indeclinable higher compounds of Jarošová 2021: `sto jeden tretinám`, `dvetisíc jeden tretinami`, `jeden milión
jeden tretinách`; `DONE-2026-10-19(2).md`), on 2026-10-16 after closing NTS-40 and NTS-41 together (one declined-cardinal builder per language,
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

None. There is no remaining open NTS item as of the 2026-10-24 audit (`DONE-2026-10-24.md`).


NTS-01 through NTS-05, NTS-08, NTS-09, NTS-10 through NTS-28, NTS-29, NTS-30, NTS-30A, NTS-30B, NTS-30C, NTS-30D, NTS-30E, NTS-30F, NTS-30G, NTS-30H, NTS-31, NTS-32, NTS-33, NTS-34, NTS-35, NTS-37, NTS-38, NTS-39, NTS-40, NTS-41, NTS-42, NTS-48, NTS-49, NTS-50, NTS-51, NTS-52, NTS-53, NTS-54 and NTS-55 are closed:

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
- NTS-42 (opened by NTS-40/41) — **closed by NTS-47**. All source-backed readings have been implemented: CS
  and SK from 10^12 (NTS-42 F, `DONE-2026-10-20.md`), SK bare final *jeden* (NTS-44, `DONE-2026-10-19(2).md`),
  CS final *jedna* in compounds and multipliers and SK zero with a fraction noun (NTS-45, `DONE-2026-10-21.md`),
  UK zero and UK from 10^12 (NTS-46, `DONE-2026-10-21(1).md`), the SK genitive singular *-óna* of the generated
  *-ón* nouns (NTS-47, `DONE-2026-10-21(2).md`). The remaining CS/SK slash-under-external-case (D, including a SK
  zero over a denominator without a fraction noun) and CS oblique-zero (E/CS) constructions are **deliberately
  unsupported** because the available linguistic evidence does not determine one canonical output: D is not
  established (no source shows the first operand of a whole *lomeno*/*lomené* phrase under an external case), E/CS
  is attested variation (NovaMorf: *záruka na nula/nulu vadných bodů*). They throw `NotSupportedException` by
  decision; see `docs/NTS-42-deliberate-closures.md` before reopening either.
- NTS-33 (opened by NTS-30A): French ordinals are derived from the base of their last constituent, not from the
  agreed cardinal. New generic derivational base `ordinalBaseString` on `<Digit>` (`DigitType.OrdinalBaseString`),
  used only by the lowest group of a cardinal-based ordinal and by the multiplier of an `<OrdinalScale>` value: FR,
  FR-be, FR-ch hundreds `deux cents` → `deux centième`, FR-be `quatre-vingts` → `cent quatre-vingtième`. The round
  values from the million are built from their singular scale noun by `<OrdinalScale scales="2..6">`
  (`millionième`, `deux millionième`, `trois cent millionième`, `dix milliardième`, `deux billionième`), the masculine
  generated nouns forcing `gender=masculin` on the multiplier (`vingt et un millionième` in the feminine). Fractions
  unchanged (`deux centièmes`, `deux millionièmes`), cardinals byte-identical, non-round ordinals keep their leading
  groups cardinal (`deux millions unième`): `DONE-2026-10-22.md`.
- NTS-48 (opened by NTS-33): French *vingt* and *cent* agree when multiplied and ending the number or before the nouns
  *million*/*milliard*, and stay invariable before *mille* (OQLF "Pluriel de vingt, de cent et de mille"): FR `80` →
  `quatre-vingts` (with `ordinalBaseString="quatre-vingt"`, so `quatre-vingtième` is kept), `200 000` → `deux cent
  mille`, `80 000` → `quatre-vingt mille`, `200 000 000` → `deux cents millions` unchanged. Configuration only: the
  existing `<Groups onScale="1">` primitive renders the multiplier of *mille*, redeclared by FR-be (`septante`,
  `nonante`) and FR-ch (`huitante`) to replace the inherited FR table; no engine change. NTS-49 opened:
  `DONE-2026-10-22.md`.
- NTS-49 (opened by NTS-48): FR, FR-be and FR-ch write their numerals in the 1990 rectified spelling (Rectifications
  de l'orthographe, JO 6 December 1990, rule 1; CCDMD numeral list), by user decision: every element of the numeral
  is hyphenated, *et*, *cent*, *mille* and the scale nouns included (`vingt-et-un`, `deux-cent-mille`,
  `deux-millions-trois-cent-mille`, `deux-millionième`); FR 81 `quatre-vingt-un` (was `quatre vingt et un`) and 71
  `soixante-et-onze` (was `soixante onze`). The words around the numeral keep their spaces (`vingt-cinq millièmes`,
  `vingt-et-un virgule cinq dixièmes`, `vingt-et-un euros`). Configuration only: hyphens in the digit tables and
  exceptions, `NumberScale`/`OrdinalScale multiplierSeparator="-"`, one `Replacement` for the group junction:
  `DONE-2026-10-22.md`.
- NTS-26 — **closed by documentation / attested variation; current output retained intentionally.** The Wolof nested
  multipliers keep the inner genitive *-i* (`ñaari téeméeri milyoŋ`, Guérin's genitive analysis); the form without it
  is attested too (Wolof Ajami Reader p. 192, Gaye 1980, Kosogorova 2023), decree 2005-992 does not settle the chain.
  No output change, no variant dimension; reopening criteria in `docs/NTS-26-wolof-nested-linker-choice.md`:
  `DONE-2026-10-22.md`.
- NTS-30 (cardinal defects found by NTS-27 and NTS-28; its other items closed by NTS-30A to NTS-30H) — **closed**
  with its last two items. VN: a whole zero class between two non-zero classes is read *không* + class name (SGK
  Toán 4, Chân trời sáng tạo, Bài 25: *307 000 262 Đọc số: Ba trăm linh bảy triệu không nghìn hai trăm sáu mươi hai*,
  *Ba triệu không nghìn không trăm mười ba*), through the new generic `internalZeroGroupString` (rendered only between
  two non-zero groups, every such group, a literal followed by its scale noun); the Vietnamese ordinal guard opens
  them, consecutive zero classes (deduced, not attested) staying closed. ES currency: the numeral governing the
  currency noun is attributive (DPD `uno` §2.2: *un euro*, *veintiún euros*, *treinta y un euros*), through the new
  generic `<Currency forceVariants="form=attributive">` (caller variants < language context < `CurrencyDefinition`):
  `DONE-2026-10-23.md`.
- NTS-38 (opened by NTS-30F) — **closed**. EU after Euskaltzaindia rule 203 (2025) §5, which supersedes rule 18:
  *milioigarren*, *bi milioigarren*, *milioi bat eta batgarren* (`<OrdinalScale scales="2">`), and by its general
  rule (-garren on the cardinal) the whole cardinal domain to 999 999 999 999 (*mila milioigarren*, *bi mila
  milioigarren*). ES after DPD `ordinales` §2d and DLE *milmillonésimo*: *milmillonésimo*, *milmillonésimo primero*,
  feminine *milmillonésima primera*; a multiplier of *millón* written in several words (*dos mil*, *mil uno*) stays
  closed like *ciento cincuenta* (NTS-28). Generic fix: an `<OrdinalScale>` round value of the unnamed upper half of
  a pair is read at the named scale below it. EU zero (*zerogarren*) left to NTS-50: `DONE-2026-10-23.md`.
- NTS-50 (opened by NTS-38) — **closed**. Basque ordinal of zero `zerogarren` (Euskaltzaindia rule 203 §5, "0.
  zerogarren"), formed by the general rule from the cardinal *zero*; only `BasqueOrdinalLanguageSpecifics` changed
  (domain 0–999 999 999 999), no exception configured: `DONE-2026-10-23(1).md`.
- NTS-51 (opened by NTS-30) — **closed**. ES: *de* after a currency numeral ending on *millón* (DPD `cardinales` §6:
  *un millón de euros*, *mil millones de euros*, but *un millón doscientos mil euros*, *un millón un euros*), through
  `<Currency terminalScaleConnector="de" terminalScales="2..">`, decided on the group structure. IT: *un euro*,
  *uno scellino*, *ventuno euro* (Treccani "uno", "articolo", "uno, numerali composti con"), through `<Currency>
  <Replacement oldValue="uno" newValue="un" scope="Standalone" exceptBeforeNoun="…">`; no `form` dimension added to
  Italian; *un milione di euro*, *due miliardi di euro* (Treccani "milione", "miliardo") through the same
  `terminalScaleConnector="di" terminalScales="2.."`. NTS-52 opened for the other languages: `DONE-2026-10-23(1).md`.
- NTS-52 (opened by NTS-51) — **closed**. The NTS-51 terminal-scale connector generalized into one declarative
  `<Currency><NounConnector default>` with `<Substitution value nounSeparator terminalScales numberRange numberModulo
  beforeNoun>` (most specific wins, first declared at equal specificity). FR family `de`/`d'` (Académie française,
  OQLF), CA family `de`/`d'` with the consonantal *i*/*u* declared first (UOC, UB), GL `de` (RAG), RO `de` from 20
  except after a compound ending on 1–19 (GALR 2005, `numberModulo`, user decision); PT attested (Priberam) but out of
  its 999 999 domain, documented only; the NTS-51 attributes kept as a shorthand, and ES/IT kept on it so that their
  public getters (`de`, `di`) and output are unchanged. NTS-53
  opened: `DONE-2026-10-23(2).md`.
- NTS-53 (opened by NTS-52) — **closed**. CA `milió(ns)` → `mili(ó|ons)`: *dos milions* (IEC Ortografia §4.4.2.2a,
  DIEC2); CA and GL static scale 3 `mil milió(ns)` / `mil mill(ón|óns)` removed for `pairedScalesFrom="2"`: *mil
  milions*, *dos mil cinc-cents milions*, *mil millóns* (DIEC2 "miliard", IEC Revista de Tecnologia 10, UPC; RAG "eón",
  "billón"); no generated scale exists in either (no prefix table, domain below 10^12). CA ordinals *milionè*, *dos
  milionè*, *mil milionè*, feminine *milionena* (GBU §18.5 attests *milionè* and *milionèsim*, GIEC §17.2.5 prefers
  the *-è* series; user decision), replacing *un milióè*. NTS-55 (opened then closed in the same PR, at review): the
  multiplier of the masculine *milió* / *millón* stays masculine under a feminine (GEIEC §12.3.2 *una dos-cents mil
  milionèsima*), `<ScaleForm scale="2" forceVariants>` as ES NTS-30G: *dos-cents milions*, *douscentos millóns*. RO: generic `<ScaleConnector default><Substitution value numberRange numberModulo scales>` (most specific
  wins, first declared at equal specificity, decided on the whole multiplier, a paired one included) refining the kept
  `scaleConnector="de"` shorthand (*o sută nouăsprezece mii*, *o sută douăzeci de mii*, GALR); currency *un* / *o*
  through two Standalone `<Currency><Replacement>` (DEX "unu", DOOM 3 §4.1.5), the cardinal *unu* unchanged. No
  multi-group replacement was needed. NTS-54 opened: `DONE-2026-10-23(3).md`.
- NTS-54 (opened by NTS-53) — **closed**. The Romanian scale multiplier agrees with its noun on its last constituent,
  whatever the caller's gender: feminine before *mie*/*mii* (DEX '09 s. f.; DOOM 3 *două mii*, *douăzeci și doi, f.
  douăzeci și două*; DOOM 2 §6.4 *douăsprezece mii de lei*), *douăzeci și una de mii*, *o sută una mii* after a corpus
  sample (ro.wikisource + ro.wikipedia, 11 against 2) and the existing ordinals (user decision); neuter before
  *milion*/*miliard* (DOOM 3 s. n.; *două puncte* num. + s. n. pl.), one masculine and two feminine: *douăzeci și unu de
  milioane*, *douăzeci și două de milioane*, *douăsprezece miliarde*. Configuration only: `<ScaleForm scale="1..3">`
  forcing `gen=feminin` / the new `gen=neutru`; the NTS-53 connector, ordinals and clock unchanged:
  `DONE-2026-10-24.md`.
- NTS-35 (opened by NTS-30C) — **closed**. Polish cardinals on the long scale without bound: *milion*, *miliard*,
  *bilion*, *biliard*, *trylion*, *tryliard*, *kwadrylion* … (SJP PWN; WSJP PAN), Conway components in Polish spelling
  from the attested series (*kwintylion*, *sekstylion*, *nonilion*, *decylion*, *duodecylion*), inflected by
  `PolishPluralLexicalFormSelector` through one `<GeneratedScaleForm>` per family (WSO PWN *bilion -ny, -nów*): *jeden
  milion*, *dwa miliony*, *pięć milionów*. The ordinals keep their validated domain (1–999 999): `DONE-2026-10-24.md`.
- NTS-34 (opened by NTS-30A) — **closed**. Decimal reading audited for every root language
  (`docs/NTS-34-decimal-reading-audit.md`): A (digit by digit attested, unchanged) DE, DA, NO, ES, ZH; B (named
  fractions) FR, EN; C (normative reading not representable) and D (no source) fail closed under `LanguageDefault`
  (`decimalDigitByDigitMaxDigits="0"`), the caller's `DigitByDigit` unchanged. No new primitive: `DONE-2026-10-24.md`.

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
