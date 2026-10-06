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
| BG | BG, BG-BG | SCALE-LONG | Yes | `BulgarianOrdinalLanguageSpecifics` (int range, gendered) | Yes | 15 / 12 | `gender` (standalone/masculine/feminine/neuter) | **Consulted**: ezik.bg, “Числително име” (`хиляда сто и втори`, gender endings); dumite-bg.com, entry `двехиляден` | Round thousands with a multi-word multiplier and round millions above one are declined (unverified compound adjectives) |
| HR | HR, HR-HR | SCALE-LONG | Yes (repaired) | Declarative last-word rules + `CroatianOrdinalLanguageSpecifics` (rejects unverified round scales) | Yes | 15 / 12 | none | **Consulted**: Hrvatski pravopis (IHJJ), “Višerječnice”, rule 31 (`tisuću jedan`, `tisuću tristo jedanaest`, `tri tisuće sedamsto trideset tri`, `milijun petsto trideset tisuća`, `dvije milijarde trideset tri milijuna …`; ordinals `tisuću prvi`, `tri tisuće sedamsto trideset treći`); Hrvatski jezični portal, entries `tisućiti`, `milijunti`, `milijarditi`. Reference usage for the clock (`pola dva`, `petnaest do dva`, `sat/sata/sati`) | Masculine nominative ordinals only; every round scale value other than the lexical units 1 000 / 1 000 000 / 1 000 000 000 fails closed as an ordinal. A single thousand followed by a remainder is `tisuću` wherever it stands (`milijun tisuću jedan`) |
| HU | HU, HU-HU | SCALE-LONG | Yes (repaired) | `HungarianOrdinalLanguageSpecifics` (int range) | Yes | 15 / 12 | none | Reference usage (vowel harmony `tizenegyedik`, `huszonkettedik`; hyphen above 2000; `negyed kettő`, `fél kettő`) | The former `suffix="."` pseudo-ordinals are gone |
| CS | CS, CS-CZ | SCALE-LONG | Yes | `CzechOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (standalone/mužský/ženský/střední) × `case` | **Consulted** (via search summary): Naše řeč, “Řadové číslovky” (`stý první`, `dvoustý`); reference: Internetová jazyková příručka “Časové údaje” (`půl druhé`, `čtvrt na dvě`) | Ordinals verified up to 9 999; round millions/milliards only for one |
| SK | SK, SK-SK | SCALE-LONG | Yes | `SlovakOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (mužský/ženský/stredný) × `case` | **Consulted**: teraz.sk, “Ako písať jednoslovné a viacslovné číslovky” (`stoprvý`, `dvetisícdruhý`, `päťsto dvadsiaty ôsmy`); lexika.sk / search summaries (`dvojstý`, `trojstý`, `tisíci`) | Ordinals verified up to 9 999 and one million |
| PL | PL | none | Yes (existing) | `PolishOrdinalLanguageSpecifics` + declarative | Yes | 5 / 12 | rodzaj × przypadek | Reference usage (Poradnia PWN conventions: `pięć po pierwszej`, `wpół do drugiej`, `za pięć druga`) | — |
| RU | RU | SCALE-LONG | Yes (existing) | Declarative | Yes | 15 / 12 | gender × case | Reference usage (`четверть второго`, `половина второго`, `без четверти два`, `час/часа/часов`) | Ordinal of zero `нулевой` (NTS-14) |
| UK | UK, UK-UA | SCALE-LONG | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | Reference usage (`двадцять перший`; `чверть по першій`, `пів на другу`, `чверть до другої`) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals |
| ES | ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender × form | Reference usage (RAE, *Diccionario panhispánico de dudas*, “hora”) | Exact `Convert(TimeOnly)` unchanged |
| IT | IT | SCALE-LONG | Yes: 1–1999 except 1110–1910, round thousands 2000–999000, 100001–100009, round multiples of milione/miliardo/bilione | Declarative `<OrdinalStem>`, `<OrdinalComposition>`, `<OrdinalScale>` + `ItalianOrdinalLanguageSpecifics` (domain guard) | Yes | 5 / 12 | gender | Reference usage (Accademia della Crusca: `l'una e mezzo`, `le due meno un quarto`, `le otto meno venti`) | Cardinals soldered with `<Fusion>` (NTS-10); compound ordinals by `<OrdinalStem>` (NTS-13, see below), analytic 1010 and 100001–100009 (NTS-15) and round scale ordinals (NTS-17) (see below); zero, 1110–1910 and the other non-round thousands above 1999 (NTS-15), non-round values from a million and a biliardo and above (NTS-18) fail closed as ordinals; millions and above are separate nouns joined by `e` (NTS-14, see below) |
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
| EE | EE | none | Yes (existing, prefix `etsõ`; formation unsourced, NTS-16) | Declarative + zero guard | **Deferred** | — | none | No source found | No ordinal of zero (NTS-14) |
| WO | WO | none | Yes (existing; `-ël` spelling unsourced, NTS-16) | Declarative + zero guard | **Deferred** | — | none | No consistent source found | No ordinal of zero (NTS-14) |

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
  entry). Biliardo (10^15) and trilione (10^18) stay fail-closed (NTS-18); *centomilionesimo* is
  produced by the general rule.
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
    configured `-ël` spelling of every other ordinal does not match the sources (NTS-16).
  - EE: Omniglot and Wiktionary (zero = nadeke/nanekeo; ordinals gbãtɔ, evelia, etɔ̃lia … with a
    `-lia` suffix). No source attests an ordinal of zero → fail closed. The configured `etsõ`
    prefix of every other ordinal is not attested either (NTS-16).

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
