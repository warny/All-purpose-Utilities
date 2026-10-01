# NTS-08 linguistic source record

This record documents the configurations covered by NTS-08: the 2026-09-23 regional slice (EN, FR,
CA, ID/MS) and the 2026-10-01 completion pass (every remaining natural-language configuration). Each
row states what the configuration supports, how, and on which sources the tested wording rests.

**Source status.** "Consulted" sources were read during the audit and are quoted in the `.feature`
comments. "Reference usage" means the wording follows standard school-grammar usage that was not
re-fetched during the audit; those rows are flagged so that a native-speaker review can target them.

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
| HR | HR, HR-HR | SCALE-LONG | Yes (repaired) | Declarative, every final word mapped | Yes | 15 / 12 | none | Reference usage (`dvadeset prvi`, `pola dva`, `petnaest do dva`, `sat/sata/sati`) | The former `suffix="i"` fallback (`četirii`) is gone; masculine nominative ordinals only |
| HU | HU, HU-HU | SCALE-LONG | Yes (repaired) | `HungarianOrdinalLanguageSpecifics` (int range) | Yes | 15 / 12 | none | Reference usage (vowel harmony `tizenegyedik`, `huszonkettedik`; hyphen above 2000; `negyed kettő`, `fél kettő`) | The former `suffix="."` pseudo-ordinals are gone |
| CS | CS, CS-CZ | SCALE-LONG | Yes | `CzechOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (standalone/mužský/ženský/střední) × `case` | **Consulted** (via search summary): Naše řeč, “Řadové číslovky” (`stý první`, `dvoustý`); reference: Internetová jazyková příručka “Časové údaje” (`půl druhé`, `čtvrt na dvě`) | Ordinals verified up to 9 999; round millions/milliards only for one |
| SK | SK, SK-SK | SCALE-LONG | Yes | `SlovakOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (mužský/ženský/stredný) × `case` | **Consulted**: teraz.sk, “Ako písať jednoslovné a viacslovné číslovky” (`stoprvý`, `dvetisícdruhý`, `päťsto dvadsiaty ôsmy`); lexika.sk / search summaries (`dvojstý`, `trojstý`, `tisíci`) | Ordinals verified up to 9 999 and one million |
| PL | PL | none | Yes (existing) | `PolishOrdinalLanguageSpecifics` + declarative | Yes | 5 / 12 | rodzaj × przypadek | Reference usage (Poradnia PWN conventions: `pięć po pierwszej`, `wpół do drugiej`, `za pięć druga`) | — |
| RU | RU | SCALE-LONG | Yes (existing) | Declarative | Yes | 15 / 12 | gender × case | Reference usage (`четверть второго`, `половина второго`, `без четверти два`, `час/часа/часов`) | — |
| UK | UK, UK-UA | SCALE-LONG | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | Reference usage (`двадцять перший`; `чверть по першій`, `пів на другу`, `чверть до другої`) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals |
| ES | ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender × form | Reference usage (RAE, *Diccionario panhispánico de dudas*, “hora”) | Exact `Convert(TimeOnly)` unchanged |
| IT | IT | SCALE-LONG | Yes (existing) | Declarative | Yes | 15 / 12 | gender | Reference usage (Accademia della Crusca: `l'una e mezzo`, `le due meno un quarto`) | Quarter-hour only: the compound cardinals are not orthographic yet (`venti cinque`), see TODO |
| PT | PT | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | Reference usage (Ciberdúvidas: direct convention `uma hora e quarenta e cinco`) | Single PT norm; no `menos` construction; PT-PT/PT-BR not split |
| GL | GL, gl-ES | none | Yes (existing) | Declarative | Yes | 5 / 12 | gender | Reference usage (RAG usage `a unha e media`, `as dúas menos cuarto`) | — |
| RO | RO, RO-RO | none | Yes | `RomanianOrdinalLanguageSpecifics` (DOOM) | Yes | 15 / 12 | `gen` | **Consulted**: dexonline/DOOM entries `sutălea` (`al (o) sutălea`, `a (o) suta`, `al două sutelea`) and `miilea` (`al o miilea`, `a o mia`, `al două miilea`); reference usage for the clock (`ora două`, `două fără un sfert`) | Ordinals up to 999 999 and one million (masculine); round `de mii` thousands declined |
| EL | EL | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender (feminine cardinals added) | Reference usage (`μία και μισή`, `δύο παρά τέταρτο`, feminine `τρεις`, `τέσσερις`) | Masculine cardinals (`ένας`) not modelled; feminine cardinal words mirrored in ordinal rules so ordinals are unchanged |
| FI | FI | none | Yes (existing) | Declarative | Yes | 15 / 12 | case | Reference usage (Kielitoimisto: `varttia yli yksi`, `puoli kaksi`, `varttia vaille kaksi`) | — |
| AR | AR | none | Yes, 1–99, 100, 1000 | Declarative 1–19 + `ArabicOrdinalLanguageSpecifics` | Yes | 15 / 12 | gender | Reference usage (MSA: `حادٍ وعشرون`, `الواحدة`, `الثانية إلا ربعًا`) | Contract = indefinite short nominative without article; other ordinals above 99 fail closed; cardinals 11–99 and hundreds repaired, thousands still lack the `و` connector |
| HE | HE | none | Yes: adjectives 1–10, agreeing cardinal above | Declarative | Yes | 15 / 12 | gender (standalone/zachar/nekeva) | Reference usage (Academy of the Hebrew Language: `אחד עשר` / `אחת עשרה`, `ו` before the last element; `רבע לשתיים`) | Above ten the cardinal is the intended ordinal; cardinals from 1000 are not orthographic yet (`אחד אלף`) |
| FA | FA, FA-IR | none | Yes | Declarative (`م`, `سوم`, `سی‌ام`) | Yes | 15 / 12 | none | Reference usage (Academy of Persian Language: `اول` and `یکم` both accepted) | `اول` chosen for the standalone first; compounds use `یکم` |
| TR | TR, TR-TR | SCALE-SHORT | Yes | Declarative, every final word mapped (vowel harmony) | Yes | 15 / 12 | `case` (nominative/accusative/dative) | **Consulted** (via search summary): TDK usage (`saat yediyi çeyrek geçiyor`, `saat bir buçuk`, `saat sekize çeyrek var`) | — |
| HI | HI | none | Yes (existing) | Declarative | Yes | 15 / 12 | gender | Reference usage (`सवा`, `साढ़े`, `पौने`, suppletive `डेढ़`, `ढाई`) | Cardinals 21–99 not lexicalized yet (`बीस एक`); nukta written in NFC (ढ + ़) |
| JA | JA | none | Yes (existing, prefix 第) | Declarative | Yes | 5 / 12 | none | Reference usage (`一時半`, `一時十五分`) | No 午前/午後 |
| KO | KO | none | Yes (existing, prefix 제) | Declarative | Yes | 5 / 12 | none | Reference usage; spacing per Hangeul Matchumbeop art. 43 (`한 시 십오 분`) | Native hour words are literal ClockTime patterns; no 오전/오후 |
| ZH | ZH | none | Yes (existing, prefix 第) | Declarative | Yes | 15 / 12 | none | Reference usage (`两点`, `一点半`) | `两` only in ClockTime; quarter-hour avoids the `零五分` question; no 上午/下午 |
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

## Deviations from the initial working matrix

The task's working matrix was a starting point; wherever a consulted source disagreed, the source won:

- **SK**: `stoprvý`, `tisícprvý`, `dvetisícdruhý` (one word with a following unit) instead of the first draft `stý prvý`, `tisíci prvý`.
- **TR**: 01:15 is `saat biri çeyrek geçiyor` (accusative, TDK usage) instead of `saat bir çeyrek`.
- **KO**: `한 시 십오 분` (unit noun spaced, Hangeul Matchumbeop art. 43) instead of `한 시 십오분`.
- **SW**: `saa tano na nusu` (attested by both sources) instead of the contracted `saa tano unusu`. LangMedia labels `saa tano kasorobo` as 11:45, contrary to the meaning of *kasoro* (“less”) and to SpokenSwahili (`saa nne kasorobo` = 9:45); the project follows the latter.
- **ZU**: the hour is a class-5 ordinal after `ihora` (`ihora lokuqala`) and in relative form when the noun is elided (`ligamenxe elokuqala`, `... ngaphambi kwelesibili`), following Unisa; the draft `ihora lokuqala nqo` and `... kwehora lesibili` were not adopted (Unisa writes *ngqo*, “exactly”, and only in “ngo-5 ngqo”).
- **IT**: quarter-hour step instead of five minutes, because the configured compound cardinals (`venti cinque`) are not orthographic.
- **AR / HE**: the existing `12.34` decimal scenarios asserted ungrammatical twelves (`عشرة اثنان`, `עשר שתיים`); they now expect `اثنا عشر` / `שתים עשרה`.
- **HE**: `שתיים` keeps the configuration's spelling for two; the teen form is `שתים עשרה`.

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
- Sareko Euskal Gramatika, “Orduak nola eman euskaraz”: <https://www.ehu.eus/seg/morf/5/3/3/1/7>
- Five Colleges LangMedia, Swahili telling time: <https://www.langmedia.fivecolleges.edu/resources/tanzania/basic-communications/telling-time>
- SpokenSwahili, “Telling the time in Swahili”: <https://www.spokenswahili.com/blog/telling-the-time-in-swahili/>
- Unisa, *Learn online Zulu*, Theme 4: <https://www.unisa.ac.za/static/corporate_web/Content/UnisaOpen/freeOnlineCourse/PDF/Zulu/Learn%20online%20Zulu%20-%20Theme%204.pdf>
