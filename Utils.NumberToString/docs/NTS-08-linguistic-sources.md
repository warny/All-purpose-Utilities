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
| DE | DE (and parent fallbacks) | SCALE-LONG | Yes | Declarative (German finalization hook) | Yes | 5 / 12 | genus × kasus | Existing configuration | — |
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
| RU | RU | SCALE-LONG | Yes (existing) | Declarative | Yes | 15 / 12 | gender × case | Reference usage (`четверть второго`, `половина второго`, `без четверти два`, `час/часа/часов`) | — |
| UK | UK, UK-UA | SCALE-LONG | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | Reference usage (`двадцять перший`; `чверть по першій`, `пів на другу`, `чверть до другої`) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals |
| ES | ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender × form | Reference usage (RAE, *Diccionario panhispánico de dudas*, “hora”) | Exact `Convert(TimeOnly)` unchanged |
| IT | IT | SCALE-LONG | Yes: 1–20, round tens, 100, 1000 | Declarative + `ItalianOrdinalLanguageSpecifics` (range) | Yes | 5 / 12 | gender | Reference usage (Accademia della Crusca: `l'una e mezzo`, `le due meno un quarto`, `le otto meno venti`) | Cardinals soldered with `<Fusion>` (NTS-10, see below); compound ordinals and zero fail closed (NTS-13) |
| PT | PT | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | **Consulted** for the constructions the configuration does *not* use: Ciberdúvidas, “Minutos para a hora” (`um quarto para as dez`, `dez para as três`, `três menos dez`). The direct reading itself (`uma hora e quarenta e cinco`) is a project convention not attested by that source | Deliberate direct numeric reading; `para`/`menos` constructions not produced; PT-PT/PT-BR not split |
| GL | GL, gl-ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | Reference usage (RAG usage `a unha e media`, `as dúas menos cuarto`) | — |
| RO | RO, RO-RO | none | Yes | `RomanianOrdinalLanguageSpecifics` (DOOM) | Yes | 15 / 12 | `gen` | **Consulted**: dexonline/DOOM entries `sutălea` (`al (o) sutălea`, `a (o) suta`, `al două sutelea`) and `miilea` (`al o miilea`, `a o mia`, `al două miilea`); reference usage for the clock (`ora două`, `două fără un sfert`) | Ordinals up to 999 999 and one million (masculine); round `de mii` thousands declined |
| EL | EL | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender (feminine cardinals added) | Reference usage (`μία και μισή`, `δύο παρά τέταρτο`, feminine `τρεις`, `τέσσερις`) | Masculine cardinals (`ένας`) not modelled; feminine cardinal words mirrored in ordinal rules so ordinals are unchanged |
| FI | FI | none | Yes (existing) | Declarative | Yes | 15 / 12 | case | Reference usage (Kielitoimisto: `varttia yli yksi`, `puoli kaksi`, `varttia vaille kaksi`) | — |
| AR | AR | none | Yes, 1–99, 100, 1000 | Declarative 1–19 + `ArabicOrdinalLanguageSpecifics` | Yes | 15 / 12 | gender | **Consulted** (clock): LibreTexts, *Introduction to Arabic II*, 5.6 “Ordinal Number and Telling Time” (feminine ordinal hours, `الواحدة` for one, `الحادية عشرة`, `الثانية عشرة`, quarter/half/quarter-to); search summaries of OpenArabic/Noor Sisters (`والربع`, `والنصف`, `إلا ربعًا`, next hour after subtraction). Reference usage for the ordinals above 19 | Contract = indefinite short nominative without article; other ordinals above 99 fail closed; the thousands take the attached `و` connector (NTS-10, see below), their dual/plural forms are not modelled. LibreTexts writes `وربع` / `إلا ربع`; the configuration keeps `والربع` / `إلا ربعًا` |
| HE | HE | none | Yes: adjectives 1–10, agreeing cardinal above | Declarative | Yes | 15 / 12 | gender (standalone/zachar/nekeva) | Reference usage (Academy of the Hebrew Language: `אחד עשר` / `אחת עשרה`, `ו` before the last element; `רבע לשתיים`) | Above ten the cardinal is the intended ordinal; thousands fixed by NTS-11 (see below); no ordinal of zero (NTS-12) |
| FA | FA, FA-IR | none | Yes | Declarative (`م`, `سوم`, `سی‌ام`) | Yes | 15 / 12 | none | Reference usage (Academy of Persian Language: `اول` and `یکم` both accepted) | `اول` chosen for the standalone first; compounds use `یکم` |
| TR | TR, TR-TR | SCALE-SHORT | Yes | Declarative, every final word mapped (vowel harmony) | Yes | 15 / 12 | `case` (nominative/accusative/dative) | **Consulted** (via search summary): TDK usage (`saat yediyi çeyrek geçiyor`, `saat bir buçuk`, `saat sekize çeyrek var`) | — |
| HI | HI | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender | **Consulted** (clock): HindiPod101, “Telling the Time in Hindi” (`सवा चार`, `साढ़े छह`, `पौने बारह` = 11:45, `डेढ़` = 1:30, `ढाई` = 2:30) | Cardinals 21–99 lexicalized by NTS-10 (see below); nukta written in NFC (ढ + ़) |
| JA | JA | none | Yes (existing, prefix 第) | Declarative | Yes | 5 / 12 | none | Reference usage (`一時半`, `一時十五分`) | No 午前/午後 |
| KO | KO | none | Yes (existing, prefix 제) | Declarative | Yes | 5 / 12 | none | **Consulted**: National Institute of Korean Language (국립국어원), answer “몇 시, 몇 분으로 띄어 씁니다” (시 and 분 are dependent unit nouns); Hangeul Matchumbeop art. 43 (unit nouns spaced; attached `두시 삼십분` only allowed with numerals/order) | Native hour words are literal ClockTime patterns (all twelve tested); no 오전/오후 |
| ZH | ZH | none | Yes (existing, prefix 第) | Declarative | Yes | 15 / 12 | none | **Consulted**: elon.io, “Telling Time (点 / 分 / 刻 / 半)” (`两点`, never `二点`; `十二点` keeps `二`; minutes use `二`; `零` before single-digit minutes) | `两` only for the displayed hour 2; quarter-hour step avoids single-digit minutes (`零五分`); no 上午/下午 |
| VN | VN, VI, VI-VN | none | Yes (repaired: `thứ tư`) | Declarative | Yes | 15 / 12 | none | Reference usage (`thứ nhất`, `thứ tư`; `hai giờ kém mười lăm`) | No sáng/chiều/tối |
| ID | ID, ID-ID | SCALE-SHORT | Yes | `IndonesianOrdinalLanguageSpecifics` (`long`) | Yes | 5 / 12 | none | KBBI entries `pertama`, `lewat`, `setengah`, `kurang` | Half hour refers to the following hour |
| MS | MS, MS-MY | ID | Yes | `MalayOrdinalLanguageSpecifics` (`long`) | Yes, replaces parent | 5 / 12 | none | DBP PRPM entries `lapan`, `pukul`, `suku`, `setengah`, `bilion`, `trilion` | Half hour keeps the current hour |
| CA | CA, ca-ES | none | Yes | Declarative | Yes | 15 / 12 | `gender=femení` forced on `{hour}` | IEC, *Gramàtica essencial*, §31.2; Optimot | Traditional quarters only |
| CA-valencia | ca-ES-valencia | CA | Inherited | Declarative | Yes, replaces parent | 5 / 12 | `una` forced; invariable `dos` in ClockTime only | AVL, *Gramàtica normativa valenciana*, §32.2; AVL glossary `dos` | `dos` limited to ConvertClockTime |
| EU | EU, eu-ES | none | Yes (existing) | Declarative | Yes | 15 / 12 | none | **Consulted**: Sareko Euskal Gramatika (EHU), “Orduak nola eman euskaraz” (`ordu bata`, `ordu bat eta erdiak` with `*ordu bata eta erdiak` marked wrong, `ordu bata eta laurden`, `bostak laurden gutxi`) | Clock-case forms are literal per hour; cardinals unchanged |
| SW | SW, SW-KE, SW-TZ | SCALE-SHORT | **Deferred** | — | Yes | 15 / 12, `hourOffset=-6` | none | **Consulted**: Five Colleges LangMedia, “Swahili – Tanzania – Telling Time”; SpokenSwahili, “Telling the time in Swahili” | No day-part words (asubuhi, mchana, jioni, usiku) |
| ZU | ZU | none | **Deferred** | — | Yes | 15 / 12 | none | **Consulted**: Unisa, *Learn online Zulu*, Theme 4 (`Yihora lesihlanu`, `Ligamenxe elesihlanu`, `... lishayile elesihlanu`, `... ngaphambi kwelesihlanu`) — single source | Hour forms are literal ClockTime patterns; they do not enable ordinals |
| EE | EE | none | Yes (existing, prefix `etsõ`) | Declarative | **Deferred** | — | none | No source found | — |
| WO | WO | none | Yes (existing) | Declarative | **Deferred** | — | none | No consistent source found | — |

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
  need a vowel-specific stem rule (NTS-13) and fail closed meanwhile. Clock: Accademia della Crusca
  (via Linkiesta) validates *le otto meno venti* / *meno dieci*, hence the five-minute step.
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
