# NTS-08 linguistic source record

This record documents the configurations covered by NTS-08: the 2026-09-23 regional slice (EN, FR,
CA, ID/MS) and the 2026-10-01 completion pass (every remaining natural-language configuration). Each
row states what the configuration supports, how, and on which sources the tested wording rests.

**Source status.** "Consulted" sources were read during the audit and are quoted in the `.feature`
comments. "Reference usage" means the wording follows standard school-grammar usage that was not
re-fetched during the audit; those rows are flagged so that a native-speaker review can target them.
A row marked "Consulted" may still be only partly sourced (e.g. HR clock, AR ordinals above 19, HI/KO/ZH
ordinals) or rest on a single isolated source (HI, ZH and ZU clocks, TR clock via a search summary);
the remaining validation and its criterion are tracked per capability in `TODO.md` (NTS-08).

## Configurations

| Configuration | Cultures | BaseOn | Ordinals | Ordinal implementation | ClockTime | Step / cycle | Variants | Sources | Limitation |
|---|---|---|---|---|---|---|---|---|---|
| EN | EN, EN-us | SCALE-SHORT | Yes | Declarative | Yes | 5 / 12 | none | Cambridge Dictionary, “Telling the time”; British Council LearnEnglish | No day-part wording. `midnight`/`noon` replace the whole wording through `specialHourPattern="{hour}"`, including after rounding (11:58 → `noon`, 23:58 → `midnight`); with `ReplaceSpecialHours = false` the normal `{hour} o'clock` pattern is used |
| EN-GB | EN-GB, EN-uk | EN | Inherited | Declarative | Inherited (no own section) | 5 / 12 (inherited) | none | Same as EN | Date format remains the only override |
| FR | FR, FR-fr, FR-ca | SCALE-LONG | Yes | Declarative | Yes | 5 / 24 | `gender=feminin` through time units | OQLF, Banque de dépannage linguistique, “Heure”; Académie française | Existing project convention retained |
| FR-be | FR-be | FR | Inherited, merged with one override (`80 → quatre-vingtième`) | Declarative | Inherited (no own section) | 5 / 24 (inherited) | inherited gender | Fédération Wallonie-Bruxelles, *Guide de rédaction* | Belgian `septante` / `quatre-vingts` / `nonante` |
| FR-ch | FR-ch | FR | Inherited (`huitantième`) | Declarative | Inherited (no own section) | 5 / 24 (inherited) | inherited gender | Dictionnaire suisse romand; TERMDAT | Project convention `huitante` (no cantonal `octante`) |
| DE | DE (and parent fallbacks) | SCALE-LONG | Yes | Declarative (German finalization hook) | Yes | 5 / 12 | genus × kasus | Existing configuration; Duden `nullte` | Ordinal of zero `nullte` (NTS-14) |
| DE-ch | de-CH, de-LI | DE | Merged | Declarative | Inherited (no own section) | 5 / 12 (inherited) | inherited | Existing configuration | — |
| NL | NL | none | Yes | Declarative | Yes | 5 / 12 | none | **Consulted**: Taaladvies.net, “Kloktijden” (`voor half`, `over half`, quarter division of the hour) and “Klemtoonteken” (`één uur`); Taaladvies on `halfacht`/`half acht` (both correct) | No day-part wording; `half twee` written in two words |
| DA | DA, DA-DK | SCALE-LONG | Yes | `DanishOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | `gender` (fælleskøn/intetkøn): neuter `et` for the clock hour | **Consulted**: sproget.dk (Dansk Sprognævn), “Den 101. dalmatiner …” (`hundrede` is the ordinal of 100 per Retskrivningsordbogen); reference usage for the vigesimal ordinals (`halvtredsindstyvende`) and the clock | Ordinals above `int.MaxValue` fail closed |
| NO | NO, NB, NB-NO | SCALE-LONG | Yes | `NorwegianOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | `gender` (hankjønn/hunkjønn/intetkjønn): neuter `ett` for the clock hour | **Consulted**: Riksmålsforbundet, *Grammatikk*, ch. 7 “Tallord” (`tjueførste`, `hundrede`, `tusende`, `millionte`, compounds written together); Språkrådet, “Tall, tid og dato” (no word forms) | Bokmål `andre` retained (Riksmål prefers `annen`) |
| SV | SV, SV-SE | SCALE-LONG | Yes | `SwedishOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | none | Reference usage (`tjugoförsta`, `hundrade`, `tusende`; `fem i halv två`) | Ordinals above `int.MaxValue` fail closed |
| BG | BG, BG-BG | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `BulgarianOrdinalLanguageSpecifics` (int range, gendered) | Yes | 15 / 12 | `gender` (standalone/masculine/feminine/neuter) | **Consulted**: ezik.bg, “Числително име” (`хиляда сто и втори`, gender endings); dumite-bg.com, entry `двехиляден` | Round thousands with a multi-word multiplier and round millions above one are declined (unverified compound adjectives) |
| HR | HR, HR-HR | SCALE-LONG | Yes (repaired) | Declarative last-word rules + `CroatianOrdinalLanguageSpecifics` (rejects unverified round scales) | Yes | 15 / 12 | none | **Consulted**: Hrvatski pravopis (IHJJ), “Višerječnice”, rule 31 (`tisuću jedan`, `tisuću tristo jedanaest`, `tri tisuće sedamsto trideset tri`, `milijun petsto trideset tisuća`, `dvije milijarde trideset tri milijuna …`; ordinals `tisuću prvi`, `tri tisuće sedamsto trideset treći`); Hrvatski jezični portal, entries `tisućiti`, `milijunti`, `milijarditi`. Reference usage for the clock (`pola dva`, `petnaest do dva`, `sat/sata/sati`) | Masculine nominative ordinals only; every round scale value other than the lexical units 1 000 / 1 000 000 / 1 000 000 000 fails closed as an ordinal. A single thousand followed by a remainder is `tisuću` wherever it stands (`milijun tisuću jedan`) |
| HU | HU, HU-HU | SCALE-LONG | Yes (repaired) | `HungarianOrdinalLanguageSpecifics` (int range) | Yes | 15 / 12 | none | Reference usage (vowel harmony `tizenegyedik`, `huszonkettedik`; hyphen above 2000; `negyed kettő`, `fél kettő`) | The former `suffix="."` pseudo-ordinals are gone |
| CS | CS, CS-CZ | SCALE-LONG | Yes | `CzechOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (standalone/mužský/ženský/střední) × `case` | **Consulted** (via search summary): Naše řeč, “Řadové číslovky” (`stý první`, `dvoustý`); reference: Internetová jazyková příručka “Časové údaje” (`půl druhé`, `čtvrt na dvě`) | Ordinals verified up to 9 999; round millions/milliards only for one |
| SK | SK, SK-SK | SCALE-LONG | Yes | `SlovakOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (mužský/ženský/stredný) × `case` | **Consulted**: teraz.sk, “Ako písať jednoslovné a viacslovné číslovky” (`stoprvý`, `dvetisícdruhý`, `päťsto dvadsiaty ôsmy`); lexika.sk / search summaries (`dvojstý`, `trojstý`, `tisíci`) | Ordinals verified up to 9 999 and one million |
| PL | PL | none | Yes (existing) | `PolishOrdinalLanguageSpecifics` + declarative | Yes | 5 / 12 | rodzaj × przypadek | Reference usage (Poradnia PWN conventions: `pięć po pierwszej`, `wpół do drugiej`, `za pięć druga`) | — |
| RU | RU | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes (existing) | Declarative | Yes | 15 / 12 | gender × case | Reference usage (`четверть второго`, `половина второго`, `без четверти два`, `час/часа/часов`) | Ordinal of zero `нулевой` (NTS-14) |
| UK | UK, UK-UA | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | Reference usage (`двадцять перший`; `чверть по першій`, `пів на другу`, `чверть до другої`) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals |
| ES | ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender × form | Reference usage (RAE, *Diccionario panhispánico de dudas*, “hora”) | Exact `Convert(TimeOnly)` unchanged |
| IT | IT | SCALE-LONG | Yes: 1–1999 except 1110–1910, round thousands 2000–999000, 100001–100009, round multiples of milione/miliardo/bilione/biliardo/trilione | Declarative `<OrdinalStem>`, `<OrdinalComposition>`, `<OrdinalScale>` + `ItalianOrdinalLanguageSpecifics` (domain guard) | Yes | 5 / 12 | gender | Reference usage (Accademia della Crusca: `l'una e mezzo`, `le due meno un quarto`, `le otto meno venti`) | Cardinals soldered with `<Fusion>` (NTS-10); compound ordinals by `<OrdinalStem>` (NTS-13, see below), analytic 1010 and 100001–100009 (NTS-15) and round scale ordinals through trilione (NTS-17, NTS-18) (see below); zero, 1110–1910, the other non-round thousands above 1999 and the non-round values from a million deliberately fail closed as ordinals (Italian closure, see below); millions and above are separate nouns joined by `e` (NTS-14, see below) |
| PT | PT | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | **Consulted** for the constructions the configuration does *not* use: Ciberdúvidas, “Minutos para a hora” (`um quarto para as dez`, `dez para as três`, `três menos dez`). The direct reading itself (`uma hora e quarenta e cinco`) is a project convention not attested by that source | Deliberate direct numeric reading; `para`/`menos` constructions not produced; PT-PT/PT-BR not split |
| GL | GL, gl-ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | Reference usage (RAG usage `a unha e media`, `as dúas menos cuarto`) | — |
| RO | RO, RO-RO | none | Yes | `RomanianOrdinalLanguageSpecifics` (DOOM) | Yes | 15 / 12 | `gen` | **Consulted**: dexonline/DOOM entries `sutălea` (`al (o) sutălea`, `a (o) suta`, `al două sutelea`) and `miilea` (`al o miilea`, `a o mia`, `al două miilea`); reference usage for the clock (`ora două`, `două fără un sfert`) | Ordinals up to 999 999 and one million (masculine); round `de mii` thousands declined |
| EL | EL | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender (feminine cardinals added) | Reference usage (`μία και μισή`, `δύο παρά τέταρτο`, feminine `τρεις`, `τέσσερις`) | Masculine cardinals (`ένας`) not modelled; feminine cardinal words mirrored in ordinal rules so ordinals are unchanged |
| FI | FI | none | Yes (existing) | Declarative | Yes | 15 / 12 | case | Reference usage (Kielitoimisto: `varttia yli yksi`, `puoli kaksi`, `varttia vaille kaksi`) | — |
| AR | AR | none | Yes, 1–99, 100, 1000 | Declarative 1–19 + `ArabicOrdinalLanguageSpecifics` | Yes | 15 / 12 | gender | **Consulted** (clock): LibreTexts, *Introduction to Arabic II*, 5.6 “Ordinal Number and Telling Time” (feminine ordinal hours, `الواحدة` for one, `الحادية عشرة`, `الثانية عشرة`, quarter/half/quarter-to); search summaries of OpenArabic/Noor Sisters (`والربع`, `والنصف`, `إلا ربعًا`, next hour after subtraction). Reference usage for the ordinals above 19 | Contract = indefinite short nominative without article; other ordinals above 99 fail closed; the thousands take the attached `و` connector (NTS-10) and the form their multiplier governs (NTS-14, see below). LibreTexts writes `وربع` / `إلا ربع`; the configuration keeps `والربع` / `إلا ربعًا` |
| HE | HE | none | Yes: adjectives 1–10, agreeing cardinal above | Declarative | Yes | 15 / 12 | gender (standalone/zachar/nekeva) | Reference usage (Academy of the Hebrew Language: `אחד עשר` / `אחת עשרה`, `ו` before the last element; `רבע לשתיים`) | Above ten the cardinal is the intended ordinal, masculine by default also when a compound ends in a teen (NTS-14); thousands fixed by NTS-11 (see below); no ordinal of zero (NTS-12) |
| FA | FA, FA-IR | none | Yes | Declarative (`م`, `سوم`, `سی‌ام`) | Yes | 15 / 12 | none | Reference usage (Academy of Persian Language: `اول` and `یکم` both accepted) | `اول` chosen for the standalone first; compounds use `یکم` |
| TR | TR, TR-TR | SCALE-SHORT | Yes | Declarative, every final word mapped (vowel harmony) | Yes | 15 / 12 | `case` (nominative/accusative/dative) | **Consulted** (via search summary): TDK usage (`saat yediyi çeyrek geçiyor`, `saat bir buçuk`, `saat sekize çeyrek var`) | — |
| HI | HI | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender | **Consulted** (clock): HindiPod101, “Telling the Time in Hindi” (`सवा चार`, `साढ़े छह`, `पौने बारह` = 11:45, `डेढ़` = 1:30, `ढाई` = 2:30) | Cardinals 21–99 lexicalized by NTS-10 (see below); nukta written in NFC (ढ + ़) |
| JA | JA | none | Yes (existing, prefix 第) | Declarative | Yes | 5 / 12 | none | Reference usage (`一時半`, `一時十五分`) | No 午前/午後 |
| KO | KO | none | Yes (existing, prefix 제) | Declarative | Yes | 5 / 12 | none | **Consulted**: National Institute of Korean Language (국립국어원), answer “몇 시, 몇 분으로 띄어 씁니다” (시 and 분 are dependent unit nouns); Hangeul Matchumbeop art. 43 (unit nouns spaced; attached `두시 삼십분` only allowed with numerals/order) | Native hour words are literal ClockTime patterns (all twelve tested); no 오전/오후 |
| ZH | ZH | none | Yes (existing, prefix 第) | Declarative | Yes | 15 / 12 | none | **Consulted**: elon.io, “Telling Time (点 / 分 / 刻 / 半)” (`两点`, never `二点`; `十二点` keeps `二`; minutes use `二`; `零` before single-digit minutes) | `两` only for the displayed hour 2; quarter-hour step avoids single-digit minutes (`零五分`); no 上午/下午 |
| VN | VN, VI, VI-VN | none | Yes (repaired: `thứ tư`) | Declarative | Yes | 15 / 12 | none | Reference usage (`thứ nhất`, `thứ tư`; `hai giờ kém mười lăm`) | No sáng/chiều/tối |
| ID | ID, ID-ID | SCALE-SHORT | Yes | `IndonesianOrdinalLanguageSpecifics` (`long`) | Yes | 5 / 12 | none | KBBI entries `pertama`, `lewat`, `setengah`, `kurang` | Half hour refers to the following hour |
| MS | MS, MS-MY | ID | Yes | `MalayOrdinalLanguageSpecifics` (`long`) | Yes, replaces parent | 5 / 12 | none | DBP PRPM entries `lapan`, `pukul`, `suku`, `setengah`, `bilion`, `trilion` | Half hour keeps the current hour |
| CA | CA, ca-ES | none | Yes | Declarative | Yes | 15 / 12 | `gender=femení` forced on `{hour}` | IEC, *Gramàtica essencial*, §31.2; Optimot | Traditional quarters only; no ordinal of zero (NTS-14) |
| CA-valencia | ca-ES-valencia | CA | Inherited | Declarative | Yes, replaces parent | 5 / 12 | `una` forced; invariable `dos` in ClockTime only | AVL, *Gramàtica normativa valenciana*, §32.2; AVL glossary `dos` | `dos` limited to ConvertClockTime |
| EU | EU, eu-ES | none | Yes (existing) | Declarative | Yes | 15 / 12 | none | **Consulted**: Sareko Euskal Gramatika (EHU), “Orduak nola eman euskaraz” (`ordu bata`, `ordu bat eta erdiak` with `*ordu bata eta erdiak` marked wrong, `ordu bata eta laurden`, `bostak laurden gutxi`) | Clock-case forms are literal per hour; cardinals unchanged |
| SW | SW, SW-KE, SW-TZ | SCALE-SHORT | **Deferred** | — | Yes | 15 / 12, `hourOffset=-6` | none | **Consulted**: Five Colleges LangMedia, “Swahili – Tanzania – Telling Time”; SpokenSwahili, “Telling the time in Swahili” | No day-part words (asubuhi, mchana, jioni, usiku) |
| ZU | ZU | none | **Deferred** | — | Yes | 15 / 12 | none | **Consulted**: Unisa, *Learn online Zulu*, Theme 4 (`Yihora lesihlanu`, `Ligamenxe elesihlanu`, `... lishayile elesihlanu`, `... ngaphambi kwelesihlanu`) — single source | Hour forms are literal ClockTime patterns; they do not enable ordinals |
| EE | EE | SCALE-SHORT (scale tables only, NTS-23) | Yes (NTS-20): cardinal + `-lia` on the last element, `gbãtɔ` for 1 | Declarative + `ZeroOrdinalUnsupportedLanguageSpecifics` | **Deferred** | — | none | **Consulted** (NTS-20): Dzablu-Kumah, *Basic Ewe for Foreign Students*; Biblica Open Ewe Contemporary Scriptures; *Ewe Basic Course* 1968; Peace Corps Togo 2010; Wiktionary | Cardinals unbounded (NTS-23): static `akpe`, then the Conway-Wechsler short scale `miliɔn`, `biliɔn`, `triliɔn` (sourced anchors), `quadriliɔn` … (project extrapolation), each before its standalone multiplier (`multiplierPosition="afterScale"`); zero ordinal unsupported |
| WO | WO | none | Yes (`-éel`, NTS-16, NTS-19) | Declarative + `WolofOrdinalLanguageSpecifics` | **Deferred** | — | none | No consistent source found | Cardinals 0–999 999; no ordinal of zero nor of the round thousands |

## NTS-25A — Localized Conway names: Cyrillic tables, SW and TR junctions (2026-10-08)

- **Project rule (generation convention, not a linguistic norm).** When a language has adopted the
  mi/bi/tri/… large-number family, the library permits a productive Conway-Wechsler extension using that
  language's own transliteration or adaptation of the prefix tables. Individual names above the forms the
  language actually uses are not claimed to be attested; what is required is a consistent transliteration.
- **Short scale with milliard (RU, BG, UK).** Russian, Bulgarian and Ukrainian count 10^12 as trillion
  (триллион, трилион, трильйон) with milliard for 10^9; the previous `SCALE-LONG` inheritance (10^12
  `биллион`/`билион`/`більйон`) was wrong and the feature tests asserting it were corrected. Consulted:
  ru.wikipedia, *Именные названия степеней тысячи* (short scale; `триллион` 10^12, `квадриллион` 10^15,
  `дециллион` 10^33, `ундециллион` 10^36, `кваттордециллион` with the variant `кваттуордециллион` 10^45,
  `седециллион`/`сексдециллион`, `септдециллион`/`септендециллион`, `центиллион` 10^303, `дуцентиллион`
  10^603; read through a page summary); Wikipedia, *Long and short scales*, "By continent — Europe"
  (Russian, Ukrainian, Bulgarian and Turkish under short scale, Russian `миллиард`/`триллион`, Turkish
  `milyar`/`trilyon`). The strict Conway variants are kept where the page lists both (`кваттуор`,
  `седеци`, `септендеци`), consistently with NTS-24.
- **Cyrillic transliteration (`SCALE-SHORT-CYRILLIC`).** Letter mapping of the NTS-24 marker table:
  c → ц, qu → кв, x → кс, i → и, terminal a → и before the junction; markers n → н, m → м, s → с,
  x → кс (two letters, written as the comma list `(н,кс,с)`/`се(кс,с)`); void group `ни`. The base
  follows the Russian spelling attested above (`кваттуор`, `дуценти`). Bulgarian and Ukrainian simplify
  the geminate (`кватуор`), as they do in their own `милион`/`мільйон` against Russian `миллион`.
  Ukrainian writes и after д, т, р, ц and і elsewhere (*правило дев'ятки* for loanwords): `вігінти`,
  `тригінта`, `квінква`, `квінгенти`, `центи`, `квадрингенти`; void group `ні`. Online Bulgarian and
  Ukrainian lists of names above the decillion could not be retrieved (bg/uk Wikipedia pages not
  found), so these spellings rest on the rule above, not on individual attestation.
- **Junctions.** RU `лли` + `он` (миллиниллион), BG `ли` + `он` (милинилион), SW `li` + `oni`
  (milinilioni) reproduce the Latin `-illi-` in both positions. UK `ль` + `йон` and TR `l` + `yon` cannot:
  the engine has one `groupSeparator` for both positions, so 10^3003 is `мільнільйон`/`milnilyon` (owner
  decision: accepted limitation).
- **Deferred to NTS-25B (owner decision).** ID/MS units/tens/hundreds tables, and the TR/SW Latin
  tables containing letters absent from those alphabets (q, x; Turkish c read /dʒ/).

## NTS-24 — Strict Conway-Guy-Wechsler scale names (2026-10-08)

- **Convention.** Strict Conway-Guy-Wechsler, a project decision: `quinquadecillion`, `sedecillion`,
  `novendecillion` are kept (not the dictionary `quindecillion`, `sexdecillion`, `novemdecillion`).
- **Marker table.** Units `un`, `duo`, `tre*`, `quattuor`, `quinqua`, `se*`, `septe*`, `octo`, `nove*`;
  tens `n deci`, `ms viginti`, `ns triginta`, `ns quadraginta`, `ns quinquaginta`, `n sexaginta`,
  `n septuaginta`, `mx octoginta`, `nonaginta`; hundreds `nx centi`, `n ducenti`, `ns trecenti`,
  `ns quadringenti`, `ns quingenti`, `n sescenti`, `n septingenti`, `mx octingenti`, `nongenti`. `tre`
  takes `s` before an s- or x-marked component, `se` takes `s` or `x`, `septe`/`nove` take `m` or `n`.
  The final vowel of a group becomes `i` before `-illion` (`trigintillion`; `centi` + `illion` =
  `centillion`). Above the 999th, `XilliYilliZillion` names the (10^6 X + 10^3 Y + Z)-th, with `nilli`
  for a zero group (`millinillion` 10^3003, `millinillitrillion` 10^3000012,
  `undecillinilliseptuagintasescentillisestrigintillion` 10^33002010111).
- **Sources.** Robert Munafo, *Large Numbers*, Conway-Wechsler section (marker table, `trescentillion`,
  `sexoctogintillion`, `millinillitrillion`, `sextillisexagintaquingentillion`):
  <https://www.mrob.com/pub/math/largenum.html>; Wikipedia, *Names of large numbers*, Conway-Guy section
  (marker footnotes, 10^3000012 and 10^33002010111 examples):
  <https://en.wikipedia.org/wiki/Names_of_large_numbers>. Wikipedia's 10^29629629633 example spells
  `trequadraginta` and `duecenti`, contradicting the same article's marker rules; it is not used.

## NTS-23 — Ewe scales above the million (2026-10-07)

- **Premises (ticket, not re-audited).** The linguistic audit preceding NTS-23 is taken as validated:
  `akpe` = 10^3, `miliɔn` = 10^6, `biliɔn` = 10^9, `triliɔn` = 10^12, i.e. modern Ewe follows the short
  scale, matching CLDR's compact patterns. Project decision: once these three `-liɔn` pivots are
  established, higher names follow the Conway-Wechsler short scale productively, without requiring an
  individual Ewe attestation of each `quadriliɔn`, `quintiliɔn` ….
- **Linguistic anchors vs. project extrapolation.**

  | Scale | Value | Name | Status |
  |---|---|---|---|
  | 1 | 10^3 | `akpe` | sourced linguistic anchor (static name, NTS-20) |
  | 2 | 10^6 | `miliɔn` | sourced linguistic anchor (NTS-22), now generated |
  | 3 | 10^9 | `biliɔn` | sourced linguistic anchor (ticket premise, CLDR compact) |
  | 4 | 10^12 | `triliɔn` | sourced linguistic anchor (ticket premise, CLDR compact) |
  | 5+ | 10^15 … | `quadriliɔn`, `quintiliɔn`, `sextiliɔn`, … `deciliɔn`, … `centiliɔn` | project-defined productive Conway-Wechsler continuation, not individually attested |

- **Mechanism.** No engine change: `EE` declares `baseOn="SCALE-SHORT"` and inherits its Conway-Wechsler
  prefix tables (`Scale0Prefixes`, `UnitsPrefixes`, `TensPrefixes`, `HundredsPrefixes`); it overrides the
  static names (`""`, `akpe`), `groupSeparator="li"` and the suffix `ɔn`, so each name is
  prefix + `li` + `ɔn` (`mi`+`li`+`ɔn` = `miliɔn`, `quadri`+`li`+`ɔn` = `quadriliɔn`). The effective
  `startIndex` is 0: with two static names, scale 2 is Conway n = 1. The higher names therefore follow the
  shared tables as they are. Conway's systematic forms are intended (`quinquadeciliɔn`, `sedeciliɔn`,
  `novendeciliɔn`, not the dictionary `quin-`, `sex-`, `novem-`). The shared construction was corrected
  for every SCALE-SHORT and SCALE-LONG language by NTS-24 (`undeciliɔn`, `vigintiliɔn`, `trigintiliɔn`,
  `miliniliɔn`; see `DONE-2026-10-08.md`), with no Ewe-specific configuration.
- **Domain.** The scale is unbounded (`NumberScale.IsUnbounded`, `CanNameGroup` true for every index), so
  `maxNumber` is removed, the policy of the other SCALE-SHORT languages (EN, ID, SW, TR). Cardinals accept
  any `BigInteger` (10^303 = `centiliɔn ɖeka`); ordinals keep the engine-wide `long` limit.
- **Composition and ordinals unchanged.** Scale noun first, standalone-cardinal multiplier
  (`biliɔn blaeve vɔ ɖekɛ`, `quadriliɔn alafa asieke blaasieke vɔ asieke`); `kple` before a lower group
  below 100; ordinals `-lia` on the last numeral element (`biliɔn ɖekalia`), never on the scale noun, no
  `<OrdinalScale>`.
- **`akpe akpe`.** A historical/traditional formation, attested with variable glosses in the sources
  (Peace Corps glossary "million", Bible `akpe akpewo` "thousands upon thousands"). Not produced; no
  `traditional|modern` variant.
- **CLDR RBNF, divergent and not used.** CLDR compact patterns (10^6 `miliɔn`, 10^9 `biliɔn`, 10^12
  `triliɔn`) match the retained system. CLDR `rbnf/ee.xml` (10^6 `miliɔn`, 10^9 `miliɔn akpe`, 10^12
  `biliɔn`) diverges and is not combined with it.
- **`kpakple`, a separate question.** Scale names are a nomenclature decision; connectors are a grammar
  rule, and nothing about them follows from Conway-Wechsler. Minimal search for a second strong source
  using `kpakple` as a numeral connector (large-number expression + `kpakple` + numeric remainder): none.
  The Biblica Ewe Bible has 99 occurrences, all a nominal "and" between noun phrases (`tasiaɖamwo kpakple
  sɔdola akpe wuieve`, 2CH 9:25: "chariots and 12 000 horsemen", the numeral inside the second noun
  phrase); the Leiden grammar literature describes it as an additive linker of nominal groups; Omniglot
  and Wiktionary have none. CLDR RBNF's `kpakple` remains an uncorroborated divergent rule;
  `groupConnector="kple"` and `groupConnectorThreshold="100"` are kept.

## NTS-22 — Ewe millions (2026-10-07)

- **Sources.**

  | Source | Million | Type | Date | Status |
  |---|---|---|---|---|
  | Biblica Open Ewe Contemporary Scriptures (Ghana) | `miliɔn ɖeka akpe alafa ɖeka` (1 100 000, 1CH 21:5), `miliɔn alafa eve` (200 000 000, REV 9:16) | contemporary corpus, digits in parentheses | 1988/2006/2020 | written productive use: noun first, standalone multiplier, `akpe` group after it |
  | Wiktionary, `miliɔn ɖeka` | `miliɔn ɖeka`, ordinal `miliɔn ɖekalia` | secondary | — | corroboration |
  | Omniglot | `miliɔn ɖeka` | secondary | — | corroboration |
  | CLDR `ee.xml`, long compact patterns | `miliɔn 0` (10^6–10^8), `biliɔn 0` (10^9–10^11), `triliɔn 0` (10^12–10^14) | localization data | current | formatting labels only |
  | CLDR `rbnf/ee.xml` (Gilbert Adjoyi) | `miliɔn <n>`; 10^9 `miliɔn akpe <n>`; `biliɔn` = 10^12; `kpakple` after hundred-thousands and millions | spellout rules, one contributor | current | contradicts the compact patterns above; not produced |
  | Peace Corps Togo 2010, glossary | `akpe akpe` | pedagogical, colloquial | 2010 | accepted, not produced |

- **`akpe akpe`.** The Bible uses `akpe akpewo` (plural) only for an indefinite "thousands upon
  thousands" (GEN 24:60, DAN 7:10 `Ame akpe akpewo nɔ esubɔm, ame akpe ewo teƒe akpe ewo`, ECC 4:16,
  MIC 6:7), never for an exact million; its exact millions use `miliɔn`. The glossary form is therefore
  documented as a descriptive (multiplicative) phrase, not produced.
- **Decision.** `miliɔn` is a static scale name (`<Scale value="2" string="miliɔn" />`), placed before its
  standalone-cardinal multiplier like `akpe`; the unused `<Suffixes><Suffix>miliɔn</Suffix>` is removed;
  `maxNumber="999999999"`. The groups are joined as below a million (`kple` before a lower top group
  below 100): a productive extension, since no source writes a million followed by a lower group below
  100 000. Ordinals: `-lia` on the last element (`miliɔn ɖekalia`, Wiktionary).
- **Not opened (NTS-23; superseded by the NTS-23 section above).** `biliɔn` and `triliɔn`: no written Ewe attestation (0 in the Bible; none in
  the web searches made, which only returned Omniglot/Wiktionary tables) outside CLDR's compact patterns, and CLDR's spellout rules give a different system
  (`miliɔn akpe` for 10^9, `biliɔn` for 10^12). No scale system (short or long) is claimed beyond 10^6.
  No higher names are generated (no `<Suffixes>`, no prefix tables).
- **Evidence levels.** Attested: 1 000 000, 1 100 000, 200 000 000; ordinal 1 000 000. Productive: every
  other value to 999 999 999 and every other ordinal.

## NTS-20 — Ewe cardinals rebuilt, ordinals restored (2026-10-07)

Supersedes the Ewe part of NTS-16 ("Ewe — withdrawn", below).

- **Consulted, full text**: S. W. Dzablu-Kumah, *Basic Ewe for Foreign Students*, 2nd ed., revised by
  U. Claudi and J. A. Ossey, Institute of African Studies, University of Cologne, lesson III.2 "The
  numerals" (tone-marked): `ɖeká`, `eve`, `etɔ̃`, `ene`, `atɔ̃́`, `adé`, `adrẽ́`, `enyí`, `asíéke`, `ewó`;
  `wúíɖekɛ́` … `wúíasíéke` ("wúí is a contraction of ewó"); `bláeve (bláave)`, `bláeve vɔ̌ ɖekɛ́` …
  `bláeve vɔ̌ asíéke` ("vɔ̌ means 'over'"), `bláetɔ̃` … `bláasíéke`; `alafá ɖeká` … `alafá ene`;
  `akpé ɖeká`, `akpé eve`.
- **Consulted, full text (corpus)**: *Biblica Open Ewe Contemporary Scriptures* (Ghana, 1988/2006/2020,
  eBible.org `ewe`, CC BY-SA 4.0). Its narrative books write numbers out and repeat the digits in
  parentheses (e.g. GEN 5:27 `ƒe alafa asiekɛ blaade-vɔ-asiekɛ (969)`, REV 7:4 `ame akpe alafa ɖeka
  blaene-vɔ-ene (144,000)`); the 435 pairs were extracted mechanically and counted:
  - thousands: always `akpe` + multiplier, the multiplier being the standalone cardinal (`akpe ɖeka`,
    `akpe wuieve`, `akpe blaeve-vɔ-eve`, `akpe blaene-vɔ-ɖekɛ` 41 000, `akpe alafa ɖeka blaene-vɔ-ene`
    144 000, `akpe alafa ade kple ɖeka` 601 000 in NUM 26:51);
  - `kple` after the thousands: 8/8 before a lower part below 100 (1005 `akpe ɖeka kple atɔ̃`, 1017,
    1052, 2056, 2067, 3023, 22 034), 2/140 before a lower part with hundreds (1200 `akpe ɖeka alafa
    eve`, 1254, 2172 …);
  - `kple` after the hundreds: units 8/8, ten 4/4 (`alafa ɖeka kple ewo`), teens 8/11, round tens
    35/82, tens + units 77/106; the attested ordinals 150th `ŋkeke alafa ɖeka blaatɔ̃lia` (GEN 8:3)
    and 480th `ƒe alafa ene blaenyilia` (1KI 6:1) have none;
  - spellings: `adre` 591 / `adrɛ` 1; `wuiɖekɛ` 40 / `wuiɖeka` 0; `-vɔ-ɖekɛ` in every 21/31/41/61;
    `asiekɛ` 108 / `asieke` 31; tens-units hyphenated (`-vɔ-` 445) or spaced;
  - ordinals: `evelia` 244, `etɔ̃lia` 103, `adrelia` 83, `ewolia` 76, `enelia`, `atɔ̃lia`, `adelia`,
    `enyilia`, `asiekelia` 6 / `asiekɛlia` 15, `wuiɖekɛlia`, `wuievelia`, `wuiasiekelia`, `blaevelia`,
    `blaeve-vɔ-ɖekɛlia` (EXO 12:18), `blaeve-vɔ-evelia`, `blaeve-vɔ-etɔ̃lia`, `alafa ɖekalia` (NEH 5:11,
    "the hundredth part"), `gbãtɔ` 414.
- **Consulted, scanned pages read**: *Ewe Basic Course*, Indiana University 1968 (ERIC ED028444),
  pp. 123–124 (tonal transcription): `/ɖèká, ɖè/` … `/asiéke/, /enyide/`, `/ewó/`, `/wúiɖèkɛ́(a)/` …,
  `/bláavè/`, `/bláavè vo ɖeké/`, `/bla ètɔ̃/` …, `/alafa ɖeka/`; "The ordinal numerals, with the
  exception of /gbãto/ 'first', are formed by adding /-lia/ to each of the numbers" (`/èvèlia/`,
  `/ètɔ̃lia/`); glossary `alafá ɖeka` "one hundred". The forms are phonetic (contracted `bláavè`,
  `vo ve`) and are not taken as spellings.
- **Consulted, full text**: Peace Corps Togo, *Ewe O.P.L. Workbook*, 2010 (livelingua.com): 1–20
  (`ɖeka`, `wuiɖeke`, `bla eve`), 21 `Bla eve vɔ ɖeke`, 100 `Alɔfa/alafa ɖeka`, 101 `Alafa ɖeka kplé
  ɖeka`, 122 `Alafa ɖeka bla eve vɔ eve`, 1000F `akpé ɖeka`; glossary "million: akpe akpe", "nothing:
  naneke o", "none: ɖeke o", "empty: ƒuƒlu, gbɔlo", "zero: naneke o, gbɔlo". Colloquial Togolese
  spelling (`ɖeke`, `bla tɔ`, font without the nasal tilde): structure only.
- **Also consulted**: Wiktionary (category Ewe cardinal numbers: `adre`, `asieke`, `wuiɖekɛ`,
  `blaeve`, `blaeve vɔ ɖekɛ`, `alafa ɖeka`, `akpe ɖeka`, `akpe ewo`, `miliɔn ɖeka`; `adre` entry
  citing Westermann 1905, Dzablu-Kumah 2015 and Nuseline's Ewe-English Dictionary 2017, ordinal
  `adrelia`; `ɖeka` → ordinal `gbãtɔ`); Omniglot (0 `nadeke, nanekeo`; `wuiɖekɛ`, `blaeve-vɔ̃-ɖekɛ`,
  `alafa ɖeka kple ɖeka`, `akpe ɖeka`, `miliɔn ɖeka`; ordinals `evelia` … `ewolia`); Wikivoyage Ewe
  phrasebook (zero `nadɛkɛ o`, `Blaeve-vɔ-ɖeke`). The 1966 Peace Corps *Ewe Pronunciation* (ERIC
  ED152111) has no numerals. No accessible Bureau of Ghana Languages orthography was found.
- **Convention** (standard orthography without tone marks, one spelling per value):
  - `ɖeka`; `ɖekɛ` after `wui-` and after `vɔ` (Dzablu-Kumah, Bible, Wiktionary, Omniglot), `ɖeka` after
    `alafa`/`akpe` and after `kple` (Peace Corps, Omniglot, Bible `kple ɖeka`);
  - `etɔ̃`, `atɔ̃` with the nasal tilde; `adre` (Bible, Wiktionary; `adrɛ` 1968/Omniglot and `adrẽ`
    Dzablu-Kumah are transcriptions of the same nasalized vowel); `asieke` (Dzablu-Kumah, 1968,
    Wiktionary, Omniglot, Peace Corps; the Bible's `asiekɛ` is accepted, not produced);
  - `vɔ` written as a separate word (Dzablu-Kumah `vɔ̌` = tone mark, Wiktionary lemma `blaeve vɔ ɖekɛ`;
    the Bible's hyphenation `blaeve-vɔ-eve` is accepted, not produced; Omniglot's `vɔ̃` is a nasal
    mistranscription);
  - `alafa`, `akpe` before their multiplier; `kple` after the hundreds before 1–10
    (`intraGroupConnectorThreshold="11"`; before 11–99 it is optional and not produced) and after
    the thousands before a lower part below 100 (`groupConnectorThreshold="100"`);
  - zero `naneke o`: the only answer of every zero row consulted (Peace Corps glossary, Omniglot,
    Wikivoyage); `gbɔlo` is the adjective "empty" (Bible `asi gbɔlo` "empty hands", Peace Corps
    glossary) and is not produced. No consulted source has a numeral for zero distinct from this
    phrase ("nothing"), which is the standalone answer to "how many?";
  - ordinals: suffix `-lia` on the last element, `gbãtɔ` for 1 only, zero rejected
    (`ZeroOrdinalUnsupportedLanguageSpecifics`); `-lia` is never applied to `akpe`.
- **Engine decision**: the Bible shows that a multiplier of `akpe` is the standalone cardinal, so the
  Ewe configuration uses only `multiplierPosition="afterScale"`; the generic `<Groups onScale>` primitive
  added by NTS-20 is not used by Ewe (no duplicated table).
- **Evidence levels**. Attested (value or ordinal printed by a source): 1–30 and the round tens
  (Dzablu-Kumah), 100–400, 1000, 2000 (Dzablu-Kumah) and every value of the Bible pairs, among them
  101 (Peace Corps), 105, 110, 120, 122, 153, 205, 276, 500, 1005, 1017, 1052, 1200, 1254, 1260, 3000,
  10000, 12000, 20000, 22000, 100000, 120000, 144000, 601000; ordinals 1–22, 100, 150, 480. Productive (sourced rule, value not exemplified): every other value to
  999 999 and every other ordinal (notably `akpe ɖekalia` 1000th, `alafa ɖeka kple ɖekalia` 101st).
  Not validated by NTS-20: the decimal separator `kpɔ`, the fraction connector `kple`, `minus *`, the
  million (`akpe akpe` vs `miliɔn ɖeka`, outside `maxNumber`).

## NTS-19 — Wolof cardinals: orthography and thousands (2026-10-07)

Revises the Wolof part of NTS-16 (next section) for the ordinal spelling and the domain.

- **Consulted, full text**: decree 2005-992 of 21 October 2005 on the orthography and word separation
  of Wolof (copy of *Journal officiel* article 4802 at labo-styloculture.com; jo.gouv.sn unreachable).
  Art. 10: a closed long vowel takes one accent (`néeg`). Art. 12: a determiner is written apart from
  its noun. Art. 13: "Dans un syntagme déterminatif, la marque -u/-i du rapport complétant/complété est
  rattachée au terme complété" (`néegu ñax`, `ay saami kaani`, `fukkéelu garab gi` "le dixième
  arbre"). Art. 20: suffixes keep an invariable spelling despite vowel harmony. Art. 21: the elements
  of a compound word are hyphenated (`gaynde-géej` "requin", `mbaam-àll`, `xam-xam`). Art. 23: `ñaari
  tomb` "deux points". The decree has no section on numerals.
- **Consulted, full text**: P. A. Gaye, *Practical Course in Wolof: An Audio-Aural Approach*, Peace
  Corps, 1980 (ERIC ED226616), pp. 66–67: 1–100 (`juróom benn`, `fukk ag benn`, `ñaar fukk`,
  `fanweer` 30, `juróom benn fukk` 60, `téeméer` 100) and money: "Notice also the -i- between the
  number and dërëm. This -i- is a linker and indicates a relationship between the number and the
  object counted. This is true not only for money but for counting any object. With ñaar the -i- is
  optional"; `fukk-i dërëm`, `juróom-i dërëm`, `juróom benn-i dërëm`. S. Voisin, "Possession adnominale
  dans différentes variétés de wolof", *Afrikanistik-Aegyptologie-Online* 2021, quoting Fal (1999:129)
  `ñaari potu meew` "deux boîtes de lait". Robert 2021 §3.2.7 and Kosogorova 2023 §4.1 (see NTS-16).
- **Also consulted**: Boston University, The 200 Word Project, "Woññi (Numbers)" (`Tus` zero, `Ñeent`,
  `Juróóm Benn`, `Tééméér`, `Junni`, million `Fukki Téémééri Junni / Benn Milyoŋ`); Wiktionary, Wolof
  number list (`tus`, `ñeent`, `juróom benn`, `fukk ak benn`, `ñaar fukk`, `téeméer`, `junni`); Janga
  Wolof (`tus / dara`, `ñaari téeméer`, `junni`, `junni ak benn`, `ñaari junni`, `fukki junni`,
  `téeméeri junni`; ordinals `-éél`, `junniéél`); Omniglot (`ñaari temeer`, `junneel`).
- **Decisions** (detail and the thousands matrix in `DONE-2026-10-07(2).md`):
  - spaces between the words of a numeral (every grammar; Art. 21 concerns lexicalized compounds);
  - the connective `-i` on the last element of the multiplier of `téeméer` and `junni`, attached
    (Art. 13): `ñaari téeméer`, `juróom benni téeméer`, `ñaari junni`, `fukki junni`, `téeméeri junni`;
    not on the tens (`ñaar fukk`); Kosogorova's `ñaar tééméér` is the variant Gaye calls optional
    with `ñaar`, accepted but not produced;
  - `junni` alone for 1000; `ak` before the part below the thousands (`junni ak benn`);
  - zero `tus` (BU, Wiktionary, Janga): `dara` is "anything/nothing", `sero` unattested;
  - ordinal suffix `-éel` (Art. 13 `fukkéelu`, Art. 20), replacing NTS-16's `-eel`;
  - `ñent` kept (Robert, Kosogorova, Gaye), `ñeent` (BU, Wiktionary) accepted, not produced.
  - `ñett fukk` kept for 30 (Kosogorova); `fanweer` (Robert, Gaye: irregular primary number) accepted, not produced.
- **Evidence levels**. Attested (structure; the ordinal spelling follows the decree): 0, 1–10, 11–19, the round tens, 100, 101, 111, 234, 1000, 1001, 1007,
  2000, 2080, 10000, 100000; ordinals 2, 3, 7, 8, 10, 12, 24. Productive (rules stated by
  the sources, value not exemplified): the other cardinals to 999 999, notably the compound
  multipliers (`fukk ak benni junni`, `ñaar fukk ak benni junni`), and every other ordinal.
  Unsupported: the ordinal of zero and of the round thousands (`junneel` vs `junniéél`, unsettled).
  Not audited: the million (`maxNumber` stays 999 999, NTS-21); ClockTime (deferred).
## NTS-16 — Wolof and Ewe ordinals (2026-10-07)

Two different situations. Levels of evidence follow the Italian closure: **attested** (the exact
form is printed by a consulted source), **productive** (the source states a general rule and the
form applies it), **unsupported** (`NotSupportedException`).

### Wolof — corrected (`-eel`, 1–999)

> Superseded in part by NTS-19 (previous section): the decree's full text spells the suffix `-éel`, and
> the ordinals are supported from 1000 except the round thousands.

- **Consulted, full text read**: M. A. Kosogorova, "Numeral systems of Fula and Wolof: A comparison
  of morphosyntactic characteristics", *Studies in African Languages and Cultures* 57 (2023),
  141–174, §4 — §4.2: "An affix -eel is added to the last element of a cardinal numeral to turn it
  into ordinal one" (`8 juróom ñetteel`, `12 fukk(a) ak ñaareel`, `24 ñaar fukk(a) ak ñenteel`);
  after Ngom (2003): `ñaar-eel-u xarit bi`, `fukk-eel-u fas wi`, `ñett-eel-u rééw mi`; the only
  exception is 'one', suppletive `(n)jëkk < jëkka` 'to be first', in a relative construction
  (`jigéén j-u njëkk`, `xale b-u njëkk`). S. Robert (LLACAN, CNRS & INALCO), "Wolof: A grammatical
  sketch", preprint 2021 for F. Lüpke (ed.), *The Oxford guide to the Atlantic languages of West
  Africa*, §3.2.7: "With the exception of 'first' (for which a relative clause with the verb jëkk
  'to be the first' is used), ordinal numbers are obtained by suffixation of an –eel morpheme to the
  cardinal number (e.g. juróóm ñaar-eel 'seventh')".
- **Also consulted**: Janga Wolof ("–éél": `ñaaréél`, `fukkéél`; first `bu njëk`); Omniglot, Wolof
  numbers, compiled from Malherbe & Sall, Diouf (`ňaareel`, `ñetteel`, `temeereel`, `junneel`; first
  `jëk, njëk`); decree 2005-992 on Wolof orthography and word separation, as summarized by
  au-senegal.com ("La fermeture est notée par l'accent aigu"; long vowels doubled; "Lorsque la
  voyelle longue est accentuée, seule la première voyelle porte l'accent": `néeg`, `wéer`).
- **Spelling decision, `-eel`**: Kosogorova and Robert both distinguish the closed long vowel with
  accents (`tééméér`, `juróóm`, `rééw`; Robert's vowel table opposes `éé` and `ee`) and both write the
  suffix `-eel`, so the vowel is the open /ɛː/, spelled `ee` by the decree. `-éél` (Janga Wolof)
  would be a closed vowel with a double accent, contrary to the decree. `-ël` (the former
  configuration) appears in no source.
- **First, `bu njëkk`, kept**: the relative form of `jëkk` in class B (the class Robert gives to
  `benn`), as in Ngom's `xale b-u njëkk`; Janga Wolof cites `bu njëk` as the list form. The
  converter's standalone contract needs one citation form; the class-B relative is it. Double `kk`
  follows the verb `jëkk` (Robert) and Kosogorova's `njëkk`. `jëkk` alone is not used: the sources
  present it only as the verb inside the relative construction.
- **Attested**: 2, 3, 8, 10, 12, 24, 7 (and 100 by Omniglot). **Productive** (rule stated for any
  cardinal, exception 'one' only): every other value 2–999, e.g. `fukk ak benneel`, `téeméereel`,
  `ñaar téeméer ak ñett-fukk ak ñenteel`. The cardinals below 1000 agree with the sources' structure
  (Kosogorova `234 ñaar tééméér ak ñett(a) fukk(a) ak ñen(en)t`, Robert `tééméér ak benn`) except for
  the hyphens, and the engine's last-element suffix (hyphen-aware) is exactly the stated rule.
  Kosogorova does not say that ordinals are rare above the tens; she notes "the overall lack of data
  on Wolof ordinal numerals".
- **Unsupported**: zero (NTS-14, unchanged) and every value from 1000 (`WolofOrdinalLanguageSpecifics`).
  The thousands cardinal diverges from both academic sources (`benn junni benn` for 1001 vs
  `juuni ak juróom ñaar` for 1007; Robert `ñaar-i junni ak juróóm ñett fukk`; `benn junni` vs
  `junni` for 1000), and the vowel-final `junni` + `-eel` → `junneel` is exemplified by Omniglot
  only. NTS-19 tracks those cardinals, the hyphens and the zero `sero`.

### Ewe — withdrawn (pending NTS-20; superseded by NTS-20 above)

- **Consulted, scanned text read**: I. Warburton, P. Kpotufe, R. Glover, *Ewe Basic Course*, Indiana
  University African Studies Program, 1968 (ERIC ED028444): "The ordinal numerals, with the exception
  of 'first', are formed by adding /-lia/ to each of the numbers"; glossary: `-lia` "suffix used to
  form all ordinal numerals, with the exception of 'first', ex. /evelia/ 'second' but /gbãtɔ/
  'first'"; cardinals 1–19 (`ɖeka` … `asieke`, `ewo`, `wuiɖeka`, `wuieve` …), 20–29 (`blaeve`,
  `blaeve vɔ ɖeka` …), tens `bla` + unit, 100 `alafa ɖeka`. **Independent**: Omniglot (after
  Wiktionary's Ewe numerals category and afropedea): 0 `nadeke, nanekeo`, `ɖeka`, `eve`, `etɔ̃`,
  `ene`, `atɔ̃`, `ade`, `adrɛ`, `enyi`, `asieke`, `ewo`, `wuiɖekɛ` …, `blaeve`, `blaeve-vɔ̃-` + unit,
  `blaetɔ̃`, `alafa ɖeka`, `alafa ɖeka kple ɖeka`, `akpe ɖeka`; ordinals `gbãtɔ`, `evelia`, `etɔ̃lia`,
  `enelia`, `atɔ̃lia`, `ewolia`.
- **Finding**: the `etsõ` prefix is attested nowhere, and the configured cardinals differ from both
  sources in almost every family (units 1/3/5/7/9, teens, tens, the `vɔ` connector, hundreds,
  thousands order, zero). No consulted source presents `blavo eve`, `kpeɖe` or `deka akpe` as a
  dialectal or older form; the Ghana/Togo standard orthography (Bureau of Ghana Languages) could not
  be consulted and is part of NTS-20.
- **Decision**: correcting the ordinal alone would build `-lia` on wrong bases (`etolia`,
  `asealia`), and the cardinal rebuild is broader than an ordinal ticket (and the noun-first
  thousands may need configuration work). `<Ordinals>` is removed: no Ewe ordinal is produced, the
  exception `gbãtɔ` included, until NTS-20. Expected target afterwards: `<Ordinals suffix="lia">`
  with `<OrdinalException value="1" string="gbãtɔ" />` (the prefix also applied to exceptions, which
  is why the former configuration produced `etsõ gbãtõ`).
## Italian closure — NTS-15 and NTS-18 (2026-10-07)

After this pass every Italian ordinal category has a deliberate contract: **supported** when the
form is sourced or follows a productive rule established by the sources, **unsupported**
(`NotSupportedException`) when no canonical composition or spelling could be established. The
rejected families are deliberate linguistic limitations, not technical debt: the engine can
already express them (`<OrdinalComposition>`), but generating one of several unsourced candidates
would be a choice made by intuition. No engine primitive was added.

Three levels of evidence are distinguished.

- **Explicitly attested as ordinals** — *millesimo*, *duemillesimo*, *diecimillesimo*,
  *centomillesimo* (Treccani vocabolario *ordinale*, *centomillesimo*); *milionesimo*,
  *miliardesimo*, *bilionesimo*, *decimilionesimo (o diecimilionesimo)* (Treccani vocabolario);
  *duemilionesimo* (CNR press release on the .it registry); *trilionesimo* (Nuovo De Mauro, read:
  "agg.num.ord., s.m. [1987] che in una serie ordinata occupa il posto corrispondente al trilione";
  GDLI, "agg. numerale ordinale di un trilione", cited from the task brief, not re-read);
  *millesimo decimo*, *centomillesimoprimo … centomillesimonono* (Treccani *ordinale*,
  *centomillesimo*).
- **Lexically attested and licensed by the productive ordinal rule** — *biliardesimo*. Consulted:
  ADN 2017, vol. I, Italian translation by the Ministero delle Infrastrutture e dei Trasporti
  (ECE/TRANS/258, §1.2.2.1 "Unità di misura"; the translation states it has no legal force), table
  of the decimal multiples and submultiples, read in the PDF: the factors 10^18 … 10 are named
  *Trilione, Biliardo, Bilione, Miliardo, Milione, Mille, Cento, Dieci* and the factors 10^-1 …
  10^-18 *Decimo, Centesimo, Millesimo, Milionesimo, Miliardesimo, Bilionesimo, Biliardesimo,
  Trilionesimo* (10^-15 *Biliardesimo*, 10^-18 *Trilionesimo*). The table names submultiples, i.e.
  the fractional reading ("un biliardesimo" = 10^-15): it confirms the lexical form, not its use as
  an ordinal — the ordinal/fraction distinction this document makes elsewhere. The ordinal status
  follows from the general Treccani rule already retained ("dal tema del corrispondente cardinale
  [...] con l'aggiunta della terminazione -èsimo"; *biliardo* → *biliardesimo*, as *miliardo* →
  *miliardesimo*). The Nuovo De Mauro has no entry *biliardesimo* (page not found); Italian
  Wikipedia "Biliardo (numero)" lists the ordinal *Biliardesimo* without a source (corroboration
  only, not a source). For *trilionesimo* the same table adds a second lexical attestation.
- **Productivity retained** — *duemilionesimo* is attested, and *duemiliardesimo*,
  *duebilionesimo*, *duebiliardesimo*, *duetrilionesimo*, *ventunobiliardesimo*,
  *ventitrebiliardesimo*, *novetrilionesimo* … apply the same rule already retained for the thousands
  and the millions (Treccani *ordinale*: "dal tema del corrispondente cardinale [...] con l'aggiunta
  della terminazione -èsimo", *duemillesimo*, *diecimillesimo*): the multiplier's cardinal soldered
  to the ordinal of the singular scale noun, multiplier one dropped. These multiplier forms are not
  individually attested. Junctions checked by test: the inner *tre* loses its accent through the
  existing *trébili* replacement (*ventitrebiliardesimo*); the trilione multiplier is at most 9 in
  the `long` domain (9 × 10^18 ≤ `long.MaxValue` < 10 × 10^18), so no *trétri* junction can arise and
  no replacement was added (*tretrilionesimo*); compounds of *uno* keep the plural-noun form
  *ventuno* (*ventunobiliardesimo*), not the apocopated *ventun*.
- **Not canonical / not established (rejected)** — 1110–1910: two splits are conceivable
  (*millesimo centodecimo*, *millecentesimo decimo*, or a soldered *millecentodecimo*) and no source
  selects one. Other non-round thousands above 1999 (*duemillesimo primo*? *duemillesimoprimo*?
  *duemillesimo decimo*? *ventunomillesimo primo*?): among the consulted Treccani material, the
  juxtaposition above a thousand is explicitly exemplified only for *millesimo* (*ordinale*:
  "pressoché l'unico [...] *millesimo primo, millesimo secondo, millesimo decimo*") and
  *centomillesimo* (*centomillesimoprimo …*). The absence of other examples does not show that
  Treccani excludes other heads; it means no consulted source establishes their form or spelling
  (separate or soldered), and the synthetic derivation is the partitive (*centomiladuesimo*). Non-round values from a million (*milionesimo primo*?
  *milionesimoprimo*? *duemilionesimo primo*? *miliardesimo primo*? *bilionesimo primo*?): no
  dictionary or grammar gives a form. An Italian Language Stack Exchange answer proposes
  *unmilioneunesimo* or *milionesimoprimo* for 1 000 001 while stating that it is a virtual
  extrapolation — evidence that the form is not stabilised, not a source for it.

**Searched without result (2026-10-07)**: web searches for *duemillesimo primo*, *duemillesimoprimo*,
*duemillesimo decimo*, *diecimillesimo primo*, *ventunomillesimo primo*, *millesimo centodecimo*,
*millecentesimo decimo*, *millecentodecimo*, *milionesimo primo*, *milionesimoprimo*,
*duemilionesimo primo*, *miliardesimo primo*; Treccani vocabolario *ordinale* re-read for a
productivity statement beyond *millesimo* or a segmentation rule for 1110 (none: its examples stop
at *millesimo decimo*). Only learner pages and the repository's own pull requests matched.

Guard: `ItalianOrdinalLanguageSpecifics` takes the largest validated scale unit (10^6 … 10^18, a
static table) not above the value and rejects it unless the value is a multiple of that unit; the
scan never multiplies, so it cannot overflow up to `long.MaxValue`. Round values 10^15 … 9 × 10^18
are accepted; 10^15 + 1, 10^18 + 1, 9 × 10^18 + 1 and `long.MaxValue` are rejected.

## NTS-15 and NTS-17 (2026-10-06)

Worked together because both hit the same structural gap: the historical pipeline transforms the
complete cardinal, whereas these ordinals are built from ordinal constituents (a scale noun, or two
juxtaposed ordinals). Two generic primitives were added (`<OrdinalScale>`, `<OrdinalComposition>`);
the decomposition is numeric, never a rewrite of the cardinal text.

**Ordinal versus fraction.** In Italian the `-esimo` forms are both ordinals and fraction
denominators. Treccani, vocabolario *centomillesimo*: "i successivi sono: *centomillesimoprimo*,
*centomillesimosecondo*, *centomillesimoterzo*, ecc.", and, "come partitivo, *un centomiladuesimo*,
*un centomilatreesimo*, ecc." — the synthetic derivation of a non-round thousand is the partitive
(fraction), not the ordinal. Likewise the mechanical ordinal of the cardinal *un milione* is the
fraction *un milionesimo* (Treccani *milionesimo*: "Con valore frazionario [...] un m."), while the
ordinal is *milionesimo*. Neither primitive derives the ordinal from the assembled cardinal; the
tests pin the ordinal forms and the guard keeps every unsourced value closed.

- **Round scale values (NTS-17, closed for milione, miliardo, bilione)** — **Consulted**: Treccani
  vocabolario *milionesimo* ("occupa il posto corrispondente a un milione"), *miliardesimo*,
  *bilionesimo* (*bilione* = 10^12, NTS-14), *decimilionesimo* ("Decimilionèṡimo (o
  diecimilionèṡimo) agg. num. ord."); Treccani vocabolario *ordinale* (above ten the ordinal is formed
  "dal tema del corrispondente cardinale [...] con l'aggiunta della terminazione -èsimo":
  *millesimo, duemillesimo, ... diecimillesimo*); Treccani *Enciclopedia dell'Italiano*, "numerali
  [prontuario]" (*milionesimo* among the -èsimo ordinals); CNR press release on the .it registry,
  relayed by Corecom Lazio and La Provincia di Como: "Il duemilionesimo indirizzo web a 'targa
  italiana' attivato dal Registro .it" and "il milionesimo dominio '.it'". Rule: the multiplier one
  is dropped (*milionesimo*); a larger multiplier is its cardinal soldered to the ordinal of the
  noun, as for the thousands (*duemilionesimo*, *diecimilionesimo*). Treccani accepts
  *decimilionesimo* and *diecimilionesimo*; the project writes *diecimilionesimo*, transparent on the
  configured cardinal *dieci*. Compounds of *uno* and *tre* follow the thousands chosen by NTS-13
  (*ventunomillesimo*, *ventitremillesimo*): *ventunomilionesimo*, *ventitremilionesimo* (inner *tre*
  unaccented); these multiplier forms apply the general Treccani rule and are not individually
  attested. Feminine through the variant suffix (*milionesima*, *duemilionesima*). **Searched
  without result**: Treccani vocabolario *biliardesimo*, *trilionesimo* and *centomilionesimo* (no
  entry). Biliardo (10^15) and trilione (10^18) stayed fail-closed (NTS-18, since closed: see the
  Italian closure above); *centomilionesimo* is produced by the general rule.
- **Analytic thousands (NTS-15, partly)** — **Consulted**: Treccani vocabolario *ordinale*: a second
  formation juxtaposes "l'ordinale che indica la decina, o il centinaio, con quelli che indicano le
  unità" (*decimoprimo, ventesimoprimo, centesimoprimo*), "anche in grafia staccata"; "questo tipo di
  formazione è pressoché l'unico per alcuni ordinali per i quali l'altro tipo sarebbe sentito come
  poco eufonico (*millesimo primo, millesimo secondo, millesimo decimo*) (in questi casi la grafia
  staccata è preferita)"; Treccani vocabolario *centomillesimo* (above); DICO, Università di Messina
  (above *millesimo* the official forms are *milleunesimo, milleduesimo ecc.*, the juxtaposed
  *millesimoprimo* / *millesimo primo* being formal). Decisions: 1010 → *millesimo decimo*
  (separate spelling, Treccani; no synthetic form attested); 100001–100009 → *centomillesimoprimo …
  centomillesimonono* (soldered, Treccani); both parts agree in gender (*millesima decima*,
  *centomillesimaprima*). 1001–1009 keep the official synthetic *milleunesimo* chosen by NTS-13 (the
  change is additive). **Left fail-closed (NTS-15)**: 1110–1910, where two analytic splits are
  conceivable (*millesimo centodecimo* or *millecentesimo decimo*) and searches found neither; every
  other non-round thousand above 1999 (2001, 2010, 21001, 100010, 100100, 999999 …), for which
  neither a canonical synthetic form (*duemilaunesimo* is unattested and would be partitive by
  Treccani's *centomiladuesimo* remark) nor a sourced spelling of the analytic form (*duemillesimo
  primo* or *duemillesimoprimo*) was found. **Opened NTS-18**: non-round values from a million
  (1000001, 1001000, 2000001, 1000000001) and every value from a biliardo.

## NTS-14 (2026-10-06)

Four independent families, each fixed test-first. Sweeps added with them are structural guards only.

- **IT millions and above** — **Consulted**: Treccani, *Enciclopedia dell'Italiano*, "numerali
  [prontuario]" ("Milione e miliardo sono nomi: hanno la forma plurale [...] e, quando la cifra non
  è tonda al milione / miliardo, sono seguiti da un altro cardinale separato dalla congiunzione e";
  "un milione, due milioni; un miliardo, due miliardi; due milioni e centomila") and "uno, numerali
  composti con [prontuario]" ("I composti con milioni e miliardi ammettono solo la forma separata
  (un milione e uno, due milioni e uno ...)"; compounds of *uno* "tendono a rimanere invariati":
  hence *ventuno milioni*); DICO, Università di Messina ("Milione e miliardo si scrivono sempre
  separati dalle altre cifre, con la congiunzione e", "un milione e novantunmila"); Treccani
  vocabolario *bilione* (10^12) and *trilione* (10^18); *biliardo* (10^15) from Libreriamo and
  Wiktionary. Implemented with existing mechanisms only (scale `groupSeparator="li"`, lower-case,
  `groupConnector="e"` removed again after *mille*/*-mila*, `un` before a single scale noun). The
  Italian ordinals of a million and above stayed fail-closed (NTS-17, since resolved for the round
  values, see above): the mechanical form of *un milione* would be the fractional *un milionesimo*.
- **AR thousands** — **Consulted**: Kalimah Center, "Arabic Numbers Grammar" (100/1000 and
  multiples take a singular genitive noun; "For a long number like 28,061, the counted noun follows
  the rules of the last number written"; "ثمانية وعشرون ألفًا"; duals مائتا/ألفا decline as duals);
  al-Dirassa, "Arabic Numbers" (3-10 plural genitive, 11-99 singular accusative, "أحد عشر ألفًا",
  "ألفان", "ثلاثة آلاف", "مائة ألف"); Virtual Arabic Language Academy, decision 29 (the thousand
  conjoined after a number: "أحد وألف، عشرة وألف، مئة وألف"). Rule: the noun follows the last number
  written — 1 and 2 the noun alone (ألف, ألفان), 3-10 آلاف, 11-99 ألفًا, whole hundreds ألف (dual
  hundred in construct: مائتا ألف). Contract: standalone nominative (ألفان, not ألفين), as in the rest
  of the configuration. The multiplier counts the masculine ألف and keeps its own agreement in the
  feminine (واحد وعشرون ألفًا). Implemented by the new generic scale lexical forms
  (`ArabicScaleLexicalFormSelector` returns keys; words in XML) plus `onScale` replacements for the
  multiplier text.
- **HE compound ordinals ending in a teen** — the existing contract (Academy of the Hebrew
  Language; ordinal above ten = agreeing cardinal, masculine by default) already gives
  `מאה ואחד עשר` in the zachar variant; the default/standalone ordinal now matches it through
  ordinal-only replacements. The standalone cardinal keeps the counting teen.
- **Zero ordinals** —
  - RU `нулевой`: Wiktionary (relative adjective, Zaliznyak 1b, нулевой/нулевая/нулевое/нулевые);
    Dal records нулевой and нолевой. Gramota.ru could not be read (HTTP 403). Declined like второй;
    the mechanical "нолый" is unattested.
  - DE `nullte`: Duden, "nullte" ("Ordinalzahl zu null", forms *nullter*, *nulltes*), declined like
    *dritte*; replaces "nullste". Inherited by DE-ch.
  - CA: no normative (IEC, Optimot, TERMCAT) or pair of independent sources found for an ordinal
    of zero; "zeroè"/"zeroena" were mechanical → fail closed (shared
    `ZeroOrdinalUnsupportedLanguageSpecifics`), inherited by CA-valencia.
  - WO: Janga Wolof ("–éél" added to the cardinal: ñaaréél, fukkéél; first = bu njëk; zero = tus,
    dara); search summaries of an academic grammatical sketch (-eel). No source attests an ordinal
    of zero, and the configured cardinal "sero" is not the attested zero → fail closed. The
    configured `-ël` spelling of every other ordinal does not match the sources (NTS-16, since
    corrected to `-eel`; the guard is now `WolofOrdinalLanguageSpecifics`).
  - EE: Omniglot and Wiktionary (zero = nadeke/nanekeo; ordinals gbãtɔ, evelia, etɔ̃lia … with a
    `-lia` suffix). No source attests an ordinal of zero → fail closed. The configured `etsõ`
    prefix of every other ordinal is not attested either (NTS-16: Ewe ordinals since withdrawn
    pending the cardinal rebuild, NTS-20).

## NTS-10, NTS-11 and NTS-12 (2026-10-03)

Cardinal and zero-ordinal defects found during NTS-08, fixed test-first (scenario red first, then
configuration). Each form rests on a consulted authoritative source or on at least two independent
sources; where sources disagree, the decision is stated.

- **IT cardinals (NTS-10)** — **Consulted**: Treccani, *Enciclopedia dell'Italiano*, "numerali"
  (compounds written as one word; elision of the tens' final vowel before *uno*/*otto*: "ventuno
  [e non *ventiuno]"; with *cento* the elision is uncommon before *uno*, *otto*, *undici*; with
  *mille* it is avoided: "milleuno [non *milluno]"; "I composti di *tre* vanno accentati"); Treccani
  vocabolario "mila" (*duemila*, joined in writing), "ventuno" (*ventuno ballerine*, "ventun(o)
  ballerina sarebbe raro"; ordinal *ventunesimo*), "diciannovesimo"; Libreriamo on the accent
  (*centotré*, *milletré*, *duemilatré*). *centottanta* (elision before *ottanta*, not listed among
  the uncommon cases) is corroborated by dictionary forms such as *trecentottantaduesime*.
  Decisions: the feminine compounds stay in *-uno* (Treccani's plural-noun usage; *ventuna pagina*
  with a singular noun is also recorded and not modelled); 21000 is the regular *ventunomila*
  (*ventunmila* is also attested). The tens/hundreds junctions use the new `<Fusion>` primitive;
  the thousands junctions use Replacements because they involve a scale name. Compound ordinals
  are formed since NTS-13 (next entry). Clock: Accademia della Crusca
  (via Linkiesta) validates *le otto meno venti* / *meno dieci*, hence the five-minute step.
- **IT compound ordinals (NTS-13)** — **Consulted**: Treccani, *La grammatica italiana*, "Aggettivi
  numerali ordinali" (from 11, cardinal without its final vowel + *-esimo*: "sedici ▶ sedicesimo",
  "ventiquattro ▶ ventiquattresimo", "trentotto ▶ trentottesimo"; "Nei composti con tre la -e finale
  si conserva": "ventitré ▶ ventitreesimo", "trentatré ▶ trentatreesimo"; "Nei composti con sei la -i
  finale si conserva": "ventisei ▶ ventiseiesimo"); Treccani vocabolario "ordinale" ("undicesimo …
  ventesimo, ventunesimo, ventiduesimo, ventitreesimo, … novantanovesimo, centesimo, centunesimo, …
  millesimo, duemillesimo, … diecimillesimo"; analytic alternatives "millesimo primo, millesimo
  secondo"), "centesimo" ("centesimo primo (o, più com., centunesimo), centesimo secondo (o, più com.,
  centoduesimo)"), "centomillesimo" (ordinal of *centomila*; the following ordinals are
  "centomillesimoprimo, centomillesimosecondo", the synthetic forms *centomiladuesimo* being
  partitives); *Enciclopedia dell'Italiano*, "numerali [prontuario]" ("trentaseesimo /
  trentaseiesimo", only the second used); DICO, Università di Messina (F. Ruggiano, 2020: "le vocali
  si mantengono al di sopra di cento: centounesimo …, centoundicesimo"; "Al di sopra di millesimo gli
  ordinali divengono rarissimi; le forme ufficiali, comunque, sono milleunesimo, milleduesimo ecc.");
  Accademia della Crusca, consulenza "Quarantaquattro gatti in fila per sei…" (V. Gheno, 2016:
  *milleunesimo*).
  **Validated domain**: 1–1999 except 1010–1910, and the round thousands 2000–999000, both genders
  (the feminine only replaces *-esimo* by *-esima*; the stem rules are shared).
  **x10 family (review of #617)**: after a hundred, *dieci* keeps its lexical ordinal *decimo*
  instead of the mechanical *centodiecesimo*, which no consulted source attests — Vocabolario degli
  Accademici della Crusca, 5th ed., vol. 2 p. 753, s.v. "centesimo" § III: "Centodecimo,
  Centundicesimo, Centododicesimo ec., Centoventesimo"; Wiktionary, Appendix:Italian numbers:
  *centodecimo*, *duecentodecimo*. Treccani ("ordinale") prefers the analytic *centesimo decimo* /
  *millesimo decimo* ("la grafia staccata è preferita"), which is not modelled. 110–910 are
  therefore nine exact word rules (*centodecimo/centodecima* … *novecentodecimo/novecentodecima*);
  1010–1910 have no attested synthetic form and fail closed (NTS-15). The Crusca 5th edition also
  lists *centundicesimo*; the modern DICO *centoundicesimo*, which follows the cardinal
  *centoundici*, is kept. The sweep tests are only a mechanical guard (structural invariants); the
  forms rest on the sourced examples of `Italian.feature`, one morphological family at a time.
  **Decisions**: *-seiesimo* is canonical (Treccani grammar and prontuario; *-seesimo* rejected).
  101 is *centunesimo* (Treccani, twice, "più com.") rather than DICO's *centounesimo*, although the
  cardinal stays *centouno*; the *centouno → centun* stem extends to every hundred by analogy
  (*duecentunesimo*). The other hundreds keep their vowels as in the cardinal (*centoduesimo*,
  *centotreesimo*, *centoundicesimo*, *centoottesimo*), per DICO and Treccani's general rule.
  Round thousands follow the Treccani series *duemillesimo … diecimillesimo, ecc.*: the cardinal
  multiplier is kept unchanged and *-mila* becomes *mill-* (*ventitremillesimo*,
  *ventunomillesimo* from the chosen cardinal *ventunomila*). 1001–1999 use the official synthetic
  forms (*milleunesimo*, *milletreesimo*, *millecentunesimo*); the formal analytic *millesimo primo* is
  not modelled (since NTS-15 the analytic type is used only where no synthetic form exists: 1010,
  100001–100009, see above).
  **Left fail-closed**: zero (NTS-12 decision unchanged; Treccani records *zeresimo* only in special,
  mathematical uses); 1010–1910 (above); non-round thousands above 1999 (2001, 21001, 100001 …) because no consulted
  source gives a canonical synthetic form and Treccani gives the analytic *centomillesimoprimo* for
  100001 (NTS-15); one million and above because the cardinals were known wrong (NTS-14, since fixed; the ordinals are now tracked by NTS-17), although
  Treccani attests *milionesimo*.
- **HI cardinals 21–99 (NTS-10)** — Wiktionary `Module:number_list/data/hi` (raw data), Unicode CLDR
  RBNF `hi`, and a Hindi school counting list (schooldekho.org). The three agree on most values;
  divergent spellings follow the majority: 31 `इकतीस`, 44 `चौवालीस`, 53 `तिरपन`, 63 `तिरसठ`,
  79 `उन्यासी`, 91–99 in `-नवे`, 95 `पंचानवे`. Ordinals (`इक्कीसवाँ`) match Wiktionary.
- **AR thousands connector (NTS-10)** — Unicode CLDR RBNF `ar` (`ألف[ و>>]`) and the University of
  Montana Arabic numbers sheet: `ألف وواحد`, with the proclitic attached. The dual/plural forms of
  the thousands (`ألفان`, `ثلاثة آلاف`) are out of scope and still not modelled.
- **HE thousands (NTS-11)** — Unicode CLDR RBNF `he` (1000 `אלף`, 2000 `אלפיים`, 3000–10000 construct
  form + `אלפים`, from 11000 masculine number + `אלף`); ulpan.net ("שלושת אלפים" … "עשרת אלפים";
  "we put the ve- before the last word": `אלף מאתיים שלושים וארבע`, `אלפיים ותשע`); the Academy of
  the Hebrew Language (2020 newsletter: `אלפיים ועשרים`, not `אלפיים עשרים`). The masculine
  multiplier from 11000 rests on CLDR and on the agreement of the number with the masculine noun
  `אלף` (consistent with the masculine construct forms); the Academy's pages could not be fetched
  for an explicit 11000 example. The Academy example also contradicts CLDR's `and-feminine`
  rule set, which omits `ו` before a round ten; the configuration follows the Academy.
  **Deliberate divergence from CLDR for a round hundred after thousands**: CLDR's `and-feminine`
  (`100: מאה;`) gives `אלף מאה` for 1100, while the configuration writes `אלף ומאה`. The Academy
  rule is general — modern Hebrew uses a single `ו` before the last element of the number (its
  page "ו' החיבור במספרים": `מאה ושלושים`, `חמשת אלפים ארבע מאות וחמש עשרה`) — and a round
  hundred is that last element. m-math.co.il (Israeli primary-school mathematics, "כתיבת מספר על
  פי מילים") states it explicitly: `אלפיים ושמונה מאות`, `שלושת אלפים ושבע מאות`, `חמשת אלפים
  ושלוש מאות`, next to `אלף שלוש מאות ועשרים`. CLDR omits `ו` both before a round ten and before a
  round hundred, so following it here would contradict the Academy's `אלפיים ועשרים` as well. A
  form without `ו` (`אלף מאה`) is also found in learner material (teachmehebrew.com) and is
  acceptable colloquially, but it is not the normative form retained.
- **Zero ordinals (NTS-12)** — FI `nollas`: Wiktionary (citing *Kielitoimiston sanakirja*) and
  kieli.net. PL, ES, GL, PT, EL, HE: no ordinal of zero fitting the library contract (PL *zerowy*
  would need its full declension; ES/PT informal *ceroésimo*/*zerésimo* are not standard; EL
  *μηδενικός* is not an ordinal numeral; HE ordinal adjectives stop at ten) — they fail closed.

## Deviations from the initial working matrix

The task's working matrix was a starting point; wherever a consulted source disagreed, the source won:

- **SK**: `stoprvý`, `tisícprvý`, `dvetisícdruhý` (one word with a following unit) instead of the first draft `stý prvý`, `tisíci prvý`.
- **TR**: 01:15 is `saat biri çeyrek geçiyor` (accusative, TDK usage) instead of `saat bir çeyrek`.
- **KO**: `한 시 십오 분` (unit noun spaced, Hangeul Matchumbeop art. 43) instead of `한 시 십오분`.
- **SW**: `saa tano na nusu` (attested by both sources) instead of the contracted `saa tano unusu`. LangMedia labels `saa tano kasorobo` as 11:45, contrary to the meaning of *kasoro* (“less”) and to SpokenSwahili (`saa nne kasorobo` = 9:45); the project follows the latter.
- **ZU**: the hour is a class-5 ordinal after `ihora` (`ihora lokuqala`) and in relative form when the noun is elided (`ligamenxe elokuqala`, `... ngaphambi kwelesibili`), following Unisa; the draft `ihora lokuqala nqo` and `... kwehora lesibili` were not adopted (Unisa writes *ngqo*, “exactly”, and only in “ngo-5 ngqo”).
- **IT**: quarter-hour step at first, because the configured compound cardinals (`venti cinque`) were not orthographic; moved to five minutes once NTS-10 soldered them.
- **AR / HE**: the existing `12.34` decimal scenarios asserted ungrammatical twelves (`عشرة اثنان`, `עשר שתיים`); they now expect `اثنا عشر` / `שתים עשרה`.
- **HE**: `שתיים` keeps the configuration's spelling for two; the teen form is `שתים עשרה`.

- **HR**: the first pass expected `tisući` for 1000 and `tisuća prvi` for 1001; the Hrvatski
  pravopis and the Hrvatski jezični portal give `tisućiti` and `tisuću prvi`. The scale nouns now
  agree with their multiplier (`dvije tisuće`, `pet milijuna`, `dvije milijarde`, `milijun`).

## Deliberate limitations

- **No day-part words** anywhere (12-hour neutral clocks): the engine has no rule condition on the source-hour range, and NTS-08 does not add one.
- **FR-be / FR-ch / EN-GB / DE-ch**: ClockTime inherited unchanged; no child declares a `<ClockTime>` section.
- **CA-valencia**: invariable `dos` limited to `ConvertClockTime`.
- **Clock-only lexical forms** (KO native hours, ZH `两`, EU clock case, ZU hour forms): literal patterns per displayed hour; `Convert` and `ConvertOrdinal` are unchanged (pinned by tests).
- **Plugin ranges**: values outside an ordinal plugin's verified range fail closed with `NotSupportedException`; none returns an unchanged cardinal.
- **No new 24-hour clock**: every clock added by this pass is 12-hour; the 24-hour rounding/midnight path stays covered by the existing French tests.

## Deferred

- **SW ordinals**: Swahili ordinals are `-a` + stem with an obligatory noun-class concord (`wa kwanza`, `la pili`, `ya tatu`). No concord-free standalone form exists, and choosing one class as the default would present a false universal form. Enabling them needs a public `nounClass` dimension with a documented class inventory.
- **ZU ordinals**: same reason (class-dependent relative concords `wokuqala`, `lesibili`, `esithathu` ...). The clock uses literal class-5 forms only.
- **EE clock**: only `ga eto` / `ga eto kple afa` were known; no source for minutes was found, and a clock limited to :00 and :30 would round aggressively.
- **WO clock**: native (`waxtu`) and French-derived readings coexist; no single sourced system covering 01:00–01:45 was established.
- **EE billion and trillion** (NTS-22 → NTS-23): `biliɔn`/`triliɔn` appear only in CLDR's compact patterns, which CLDR's own Ewe spellout contradicts (10^9 `miliɔn akpe`, `biliɔn` = 10^12); `maxNumber` stays 999 999 999.
- **WO ordinals of the round thousands** (NTS-19): conflicting forms for `junni` + suffix (`junneel`, `junniéél`).
- **WO million** (NTS-19): not audited, `maxNumber` stays 999 999 (NTS-21).

## Keeping the ordinal plugins in sync

`IOrdinalLanguageSpecifics` receives no converter, so each plugin rebuilds the cardinal part it
needs. The ID/MS plugins are cross-checked against the XML cardinals (`ordinal(n) == "ke" +
cardinal(n)`); the other plugins are pinned by `NumberToStringOrdinalPluginTests` (largest
supported value, round scales, fail-closed range) and by the language `.feature` files.

## Source links

- Cambridge Dictionary: <https://dictionary.cambridge.org/grammar/british-grammar/time>
- Office québécois de la langue française: <https://vitrinelinguistique.oqlf.gouv.qc.ca/>
- Institut d'Estudis Catalans grammar: <https://geiec.iec.cat/>
- AVL glossary, entry `dos`: <https://www.avl.gva.es/glossari/dos/>
- KBBI: <https://kbbi.kemdikbud.go.id/>
- Dewan Bahasa dan Pustaka PRPM: <https://prpm.dbp.gov.my/>
- Taaladvies.net, “Kloktijden”: <https://taaladvies.net/kloktijden-acht-uur-zeventien-of-zeventien-over-acht-of-dertien-voor-halfnegen/>
- Taaladvies.net, “Klemtoonteken”: <https://taaladvies.net/klemtoonteken-algemeen/>
- sproget.dk, “Den 101. dalmatiner møder den 101. stryger”: <https://sproget.dk/sprogviden/artikler-fra-blade-og-tidsskrifter/sprogligt-politikens-sprogklumme/den-101-dalmatiner-moeder-den-101-stryger/>
- Riksmålsforbundet, “Kapittel 7 Tallord”: <https://www.riksmalsforbundet.no/grammatikk/kapittel-7-tallord/2/>
- ezik.bg, “Числително име”: <https://www.ezik.bg/mod/page/view.php?id=726>
- teraz.sk, “Ako písať jednoslovné a viacslovné číslovky”: <https://www.teraz.sk/slovencina/slovencina-jednoslovne-viacslovne-cislov/343961-clanok.html>
- Naše řeč archive: <http://nase-rec.ujc.cas.cz/archiv.php?art=5604>
- dexonline, `sutălea`: <https://dexonline.ro/definitie/sut%C4%83lea>; `miilea`: <https://dexonline.ro/definitie/miilea>
- Hrvatski pravopis, rule 31: <https://pravopis.hr/pravilo/viserjecnice/31/>
- Hrvatski jezični portal, `tisućiti`: <https://hjp.znanje.hr/index.php?id=f19nXBd%2F&show=search_by_id>; `milijarditi`: <https://hjp.znanje.hr/index.php?id=e1phURI%3D&keyword=milijarditi&show=search_by_id>
- Ciberdúvidas, “Minutos para a hora”: <https://ciberduvidas.iscte-iul.pt/consultorio/perguntas/minutos-para-a-hora-a-um-quarto-para/38623>
- LibreTexts, Arabic ordinal numbers and telling time: <https://human.libretexts.org/Bookshelves/Languages/Arabic/Introduction_to_Arabic_II_(Hanaa_Alkassas_Layla_Bahar_Al-Aloom_and_Samira_Murtada)/05:_My_Daily_Routine/5.06:_Grammar_(1)__Ordinal_Number_and_Telling_Time>
- HindiPod101, “Telling the Time in Hindi”: <https://www.hindipod101.com/blog/2020/07/31/telling-time-in-hindi/>
- National Institute of Korean Language on spacing of 시/분: <https://x.com/urimal365/status/181990030162661377>; Hangeul Matchumbeop art. 43: <https://www.scourt.go.kr/portal/gongbo/PeoplePopupView.work?gubun=24&seqNum=1545>
- elon.io, Chinese clock time: <https://elon.io/grammar/chinese-mandarin/numbers/clock-time>
- Sareko Euskal Gramatika, “Orduak nola eman euskaraz”: <https://www.ehu.eus/seg/morf/5/3/3/1/7>
- Five Colleges LangMedia, Swahili telling time: <https://www.langmedia.fivecolleges.edu/resources/tanzania/basic-communications/telling-time>
- SpokenSwahili, “Telling the time in Swahili”: <https://www.spokenswahili.com/blog/telling-the-time-in-swahili/>
- Unisa, *Learn online Zulu*, Theme 4: <https://www.unisa.ac.za/static/corporate_web/Content/UnisaOpen/freeOnlineCourse/PDF/Zulu/Learn%20online%20Zulu%20-%20Theme%204.pdf>
- Treccani, *Enciclopedia dell'Italiano*, "numerali": <https://www.treccani.it/enciclopedia/numerali_(Enciclopedia-dell'Italiano)/>
- Treccani vocabolario, "ordinale": <https://www.treccani.it/vocabolario/ordinale/>; "centomillesimo": <https://www.treccani.it/vocabolario/centomillesimo/>; "milionesimo": <https://www.treccani.it/vocabolario/milionesimo/>; "miliardesimo": <https://www.treccani.it/vocabolario/miliardesimo/>; "bilionesimo": <https://www.treccani.it/vocabolario/bilionesimo/>; "decimilionesimo": <https://www.treccani.it/vocabolario/decimilionesimo/>
- Treccani, *Enciclopedia dell'Italiano*, "numerali [prontuario]": <https://www.treccani.it/enciclopedia/numerali-prontuario_(Enciclopedia-dell'Italiano)/>
- DICO, "La forma dei numerali cardinali e ordinali": <https://dico.unime.it/ufaq/la-forma-dei-numerali-cardinali-e-ordinali/>
- CNR press release on two million .it domains, relayed by Corecom Lazio: <https://corecom.regione.lazio.it/app/uploads/www.corecomlazio.it/810-il-it-a-quota-2-milioni-italia-nona-al-mondo-sui-domini.html>; La Provincia di Como: <https://www.laprovinciadicomo.it/stories/societa-e-costume/internet-due-milioni-domini-itil-numero-raddoppiato-5-anni-o_162790_11/>
- Treccani vocabolario, "mila": <https://www.treccani.it/vocabolario/mila/>; "ventuno": <https://www.treccani.it/vocabolario/ventuno/>; "uno": <https://www.treccani.it/vocabolario/uno/>; "diciannovesimo": <https://www.treccani.it/vocabolario/diciannovesimo/>
- Libreriamo, accent of numbers ending in three: <https://libreriamo.it/lingua-italiana/italiano-numeri-scritti-3-accentati/>
- Linkiesta, Accademia della Crusca on "le otto meno un quarto": <https://www.linkiesta.it/2023/02/meglio-una-quarto-alle-otto-o-alle-otto-meno-un-quarto-risponde-la-crusca/>
- Wiktionary, Hindi number data: <https://en.wiktionary.org/wiki/Module:number_list/data/hi>
- Unicode CLDR RBNF (hi, ar, he): <https://github.com/unicode-org/cldr/tree/main/common/rbnf>
- schooldekho.org, 1 to 100 in Hindi: <https://www.schooldekho.org/school/blog/details/1-to-100-in-hindi-1496>
- University of Montana, Arabic numbers: <https://hs.umt.edu/wlc/arabic/resources/numbers.pdf>
- ulpan.net, Hebrew hundreds and thousands: <https://www.ulpan.net/hebrew-numbers-hundreds-thousands-and-more>
- Academy of the Hebrew Language newsletter (30.12.2019): <https://hebrew-academy.org.il/wp-content/uploads/Newsletter-30.12.19-new-format.pdf>
- Wiktionary, "nollas": <https://en.wiktionary.org/wiki/nollas>; kieli.net: <https://kieli.net/sana/nollas>
- Kosogorova 2023, *SALC* 57: <https://salc.uw.edu.pl/index.php/SALC/article/view/306> (doi:10.32690/SALC57.7)
- Robert, "Wolof: A grammatical sketch" (HAL preprint): <https://hal.science/hal-01513269>
- Decree 2005-992 on Wolof orthography, summary: <https://au-senegal.com/comment-ecrire-correctement-le-wolof,16261.html>
- Omniglot, Wolof numbers: <https://omniglot.com/language/numbers/wolof.htm>; Ewe numbers: <https://www.omniglot.com/language/numbers/ewe.htm>
- Janga Wolof, numbers: <https://jangawolof.org/understanding-wolof-numbers-counting-in-wolof/>
- *Ewe Basic Course* (ERIC ED028444): <https://files.eric.ed.gov/fulltext/ED028444.pdf>
- Dzablu-Kumah, *Basic Ewe for Foreign Students*, 2nd ed.: <https://philtypo3.uni-koeln.de/sites/inst_afrika/pdf/BASIC_EWE_2nd_ed.pdf>
- Biblica Open Ewe Contemporary Scriptures (eBible.org `ewe`): <https://ebible.org/details.php?id=ewe>
- Peace Corps Togo, *Ewe O.P.L. Workbook* (2010): <https://www.livelingua.com/peace-corps/Ewe/Ewe%20Course%20-2010.pdf>
- Wiktionary, Ewe cardinal numbers: <https://en.wiktionary.org/wiki/Category:Ewe_cardinal_numbers>
- Unicode CLDR, Ewe locale data: <https://github.com/unicode-org/cldr/blob/main/common/main/ee.xml>; Ewe spellout rules: <https://github.com/unicode-org/cldr/blob/main/common/rbnf/ee.xml>
- Decree 2005-992, full text (copy of the *Journal officiel*): <https://labo-styloculture.com/wolofologos/decret-n-2005-992-du-21-octobre-2005/>; original: <http://www.jo.gouv.sn/spip.php?article4802>
- Gaye, *Practical Course in Wolof* (ERIC ED226616): <https://files.eric.ed.gov/fulltext/ED226616.pdf>
- Voisin 2021, *Afrikanistik-Aegyptologie-Online*: <https://doi.org/10.18716/ojs/aaeo/2021_3478>
- Boston University, The 200 Word Project, Wolof numbers: <https://www.bu.edu/200word/wolof/numbers>
- Wiktionary, Wolof number list: <https://en.wiktionary.org/wiki/Module:number_list/data/wo>
- Janga Wolof, numbers: <https://jangawolof.org/numbers/>
