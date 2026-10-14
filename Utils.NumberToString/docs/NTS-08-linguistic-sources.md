# NTS-08 linguistic source record

This record documents the configurations covered by NTS-08: the 2026-09-23 regional slice (EN, FR,
CA, ID/MS) and the 2026-10-01 completion pass (every remaining natural-language configuration). Each
row states what the configuration supports, how, and on which sources the tested wording rests.

**Source status.** Since the NTS-08 final validation (2026-10-09, section below) every ordinal and ClockTime
capability of the table is either **Validated**, **Corrected** or **Deferred / fail-closed**; the "Sources"
column names the sources that were read, and the per-capability detail (sources, rule established, status)
is in "NTS-08 final validation". Earlier wording such as "reference usage" or "search summary" no longer
describes any capability.

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
| NL | NL | SCALE-LONG (NTS-31) | Yes | Declarative + `DutchOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | none | **Corrected** (NTS-08 final): Taaladvies "Aaneenschrijven van telwoorden", "Kloktijden", "Klemtoonteken"; Woordenlijst; NTS-31: Onze Taal 70 (2001), nl.wikipedia "Lijst van machten van tien" | No day-part wording; `half twee` written in two words; NTS-08: Cardinals one word up to *duizend*; ordinals by ending; *nulde*. NTS-31: cardinals unbounded on the long scale (*een miljoen*, *twee miljard*, *biljoen* … *quadriljoen*, then productive Conway with *-joen/-jard*); ordinals over `-long.MaxValue`..`long.MaxValue`, `long.MinValue` being rejected by the engine (*miljoenste*, *twee miljoenste*, *een miljoen eerste*; e-ANS, Besluit spellingvoorschriften 2005 rule 6.N) |
| DA | DA, DA-DK | SCALE-LONG | Yes | `DanishOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | `gender` (fælleskøn/intetkøn): neuter `et` for the clock hour | **Validated** (NTS-08 final): Dansk Sprognævn SV00000084, SV00017853; DSL "Den 101. dalmatiner"; Den Danske Ordbog (*tusinde²*, *millionte*, *milliardte*, *kvart¹*, *halv*); Systime; Langenscheidt (clock) | Ordinals above `int.MaxValue` fail closed |
| NO | NO, NB, NB-NO | SCALE-LONG | Yes | `NorwegianOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | `gender` (hankjønn/hunkjønn/intetkjønn): neuter `ett` for the clock hour | **Consulted**: Riksmålsforbundet, *Grammatikk*, ch. 7 “Tallord” (`tjueførste`, `hundrede`, `tusende`, `millionte`, compounds written together); Språkrådet, “Tall, tid og dato” (no word forms) | Bokmål `andre` retained (Riksmål prefers `annen`) |
| SV | SV, SV-SE | SCALE-LONG | Yes | `SwedishOrdinalLanguageSpecifics` (int range) | Yes | 5 / 12 | none | **Corrected** (NTS-08 final): SAOB (TJUGO, TJUGONDE, HUNDRADE, TUSENDE, MILJONTE, KVART, HALV); Språkrådet Frågelådan 27138, 31080, 21216, 22011 | Ordinals above `int.MaxValue` fail closed; NTS-08: Ordinals above one million fail closed; three identical consonants reduced (*tjugoentusende*) |
| BG | BG, BG-BG | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `BulgarianOrdinalLanguageSpecifics` (int range, gendered) | Yes | 15 / 12 | `gender` (standalone/masculine/feminine/neuter) | **Consulted**: ezik.bg, “Числително име” (`хиляда сто и втори`, gender endings); dumite-bg.com, entry `двехиляден` | Round thousands with a multi-word multiplier and round millions above one are declined (unverified compound adjectives); NTS-30B: *хиляди*, *милиона*, *милиарда* after a multiplier, *един милион*; NTS-30D: *и* before the last component of every numeral phrase (*двадесет и едно*, *хиляда и сто*, *двадесет и една хиляди*) |
| HR | HR, HR-HR | SCALE-LONG | Yes (repaired) | Declarative last-word rules + `CroatianOrdinalLanguageSpecifics` (rejects unverified round scales) | Yes | 15 / 12 | none | **Consulted**: Hrvatski pravopis (IHJJ), “Višerječnice”, rule 31 (`tisuću jedan`, `tisuću tristo jedanaest`, `tri tisuće sedamsto trideset tri`, `milijun petsto trideset tisuća`, `dvije milijarde trideset tri milijuna …`; ordinals `tisuću prvi`, `tri tisuće sedamsto trideset treći`); Hrvatski jezični portal, entries `tisućiti`, `milijunti`, `milijarditi`. Clock **validated (weakest evidence, documented decision)**: Školski rječnik hrvatskoga jezika (*sat*, *sata*, *sati*); lets-learn.eu (`pola deset`, `petnaest do devet`) | Masculine nominative ordinals only; every round scale value other than the lexical units 1 000 / 1 000 000 / 1 000 000 000 fails closed as an ordinal. A single thousand followed by a remainder is `tisuću` wherever it stands (`milijun tisuću jedan`) |
| HU | HU, HU-HU | SCALE-LONG | Yes (repaired) | `HungarianOrdinalLanguageSpecifics` (int range) | Yes | 15 / 12 | none | **Corrected** (NTS-08 final): AkH12 §291; MTA "Számok" tool (AkH12-292); Giay & Nádor 1998 (clock) | The former `suffix="."` pseudo-ordinals are gone; NTS-08: A single million or milliard keeps *egy* (*egymilliomodik*) |
| CS | CS, CS-CZ | SCALE-LONG | Yes | `CzechOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (standalone/mužský/ženský/střední) × `case` | **Validated** (NTS-08 final): IJP (ÚJČ AV ČR) id 785, 791 and slovník; cs.wiktionary paradigms; Masaryk University and CZECH ONLINE (clock) | Ordinals verified up to 9 999; round millions/milliards only for one; NTS-30B: *tisíc/milion/miliarda* agree (*dva tisíce, jeden milion, dvě miliardy, pět milionů*; IJP id 792) |
| SK | SK, SK-SK | SCALE-LONG | Yes | `SlovakOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (mužský/ženský/stredný) × `case` | **Validated** (NTS-08 final): JÚĽŠ SAV (KSSJ, PSP, OGS) for the declension and the clock (*štvrť na*, *pol*, *tri štvrte na*); teraz.sk | Ordinals verified up to 9 999 and one million; NTS-30B: cardinals in the PSP one-word spelling (*dvadsaťdva, päťsto, dvetisíc*), *milióny/miliardy* agree |
| PL | PL | none | Yes (existing) | `PolishOrdinalLanguageSpecifics` + declarative | Yes | 5 / 12 | rodzaj × przypadek | **Corrected** (NTS-08 final): ZPE (MEN) "Odmiana liczebnika i zaimka"; PWN (*wpół do*, *za pięć dwunasta*); Babbel | —; NTS-08: Ordinals 1–1999 (1000–1999 rebuilt); from 2000 fail closed |
| RU | RU | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes (existing) | Declarative + `RussianOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | gender × case | **Validated** (NTS-08 final): Правила (2006) §132–133; Ожегов, Ушаков, Ефремова (clock) | Ordinal of zero `нулевой` (NTS-14); NTS-08: Ordinals 0–1999; from 2000 fail closed |
| UK | UK, UK-UA | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | **Corrected** (NTS-08 final): Український правопис 2019 §38; goroh.pp.ua paradigms; Антоненко-Давидович (clock) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals; NTS-08: Single million/milliard without *один*; round thousands up to 10 000 only; NTS-30B: *тисяча/мільйон/мільярд* agree (*дві тисячі, два мільйони, п'ять мільйонів*) |
| ES | ES | none | Yes (existing) | Declarative + `SpanishOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender × form | **Corrected** (NTS-08 final): RAE/ASALE DPD "ordinales" and "hora" | Exact `Convert(TimeOnly)` unchanged; NTS-08: Ordinals 1–29, round tens, 100, 1000; cardinals 22/23/26 accented; other values fail closed |
| IT | IT | SCALE-LONG | Yes: 1–1999 except 1110–1910, round thousands 2000–999000, 100001–100009, round multiples of milione/miliardo/bilione/biliardo/trilione | Declarative `<OrdinalStem>`, `<OrdinalComposition>`, `<OrdinalScale>` + `ItalianOrdinalLanguageSpecifics` (domain guard) | Yes | 5 / 12 | gender | Clock **validated** (NTS-08 final): Treccani vocabolario *ora²* (full five-minute table); Accademia della Crusca (`l'una e mezzo`, `le due meno un quarto`, `le otto meno venti`) | Cardinals soldered with `<Fusion>` (NTS-10); compound ordinals by `<OrdinalStem>` (NTS-13, see below), analytic 1010 and 100001–100009 (NTS-15) and round scale ordinals through trilione (NTS-17, NTS-18) (see below); zero, 1110–1910, the other non-round thousands above 1999 and the non-round values from a million deliberately fail closed as ordinals (Italian closure, see below); millions and above are separate nouns joined by `e` (NTS-14, see below) |
| PT | PT | none | Yes (existing) | Declarative + `PortugueseOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender | **Corrected** (NTS-08 final): Priberam; Ciberdúvidas "Minutos para a hora", "Eram duas horas da tarde" | Deliberate direct numeric reading; `para`/`menos` constructions not produced; PT-PT/PT-BR not split; NTS-08: European Portuguese clock (*menos* after the half hour); ordinals 1–20, round tens, 100, 1000 |
| GL | GL, gl-ES | none | Yes (existing) | Declarative + `GalicianOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender | **Corrected** (NTS-08 final): RAG/ILG NOMIG 2003 §16.2; RAG dictionary; Xunta *Aula de Galego 1* (clock) | —; NTS-08: Ordinals 1–20, round tens, 100, 1000 |
| RO | RO, RO-RO | none | Yes | `RomanianOrdinalLanguageSpecifics` (DOOM) | Yes | 15 / 12 | `gen` | **Consulted**: dexonline/DOOM entries `sutălea` (`al (o) sutălea`, `a (o) suta`, `al două sutelea`) and `miilea` (`al o miilea`, `a o mia`, `al două miilea`); reference usage for the clock (`ora două`, `două fără un sfert`) | Ordinals up to 999 999 and one million (masculine); round `de mii` thousands declined |
| EL | EL | none | Yes (existing) | Declarative + `GreekOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | gender (feminine cardinals added) | **Corrected** (NTS-08 final): Λεξικό της Κοινής Νεοελληνικής (ordinals, μισός, παρά) | Masculine cardinals (`ένας`) not modelled; feminine cardinal words mirrored in ordinal rules so ordinals are unchanged; NTS-08: Ordinals 1–20, round tens and hundreds, 1000; 13–19 agree in both parts |
| FI | FI | none | Yes (existing) | Declarative + `FinnishOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | case | **Validated** (NTS-08 final): Aalto University "Numeroiden taivutus"; Kielitoimiston sanakirja; OPH | —; NTS-08: Ordinals: nominative only, 0–20, round tens and hundreds, 1000 |
| AR | AR | none | Yes, 1–99, 100, 1000 | Declarative 1–19 + `ArabicOrdinalLanguageSpecifics` | Yes | 15 / 12 | gender | **Consulted** (clock): LibreTexts, *Introduction to Arabic II*, 5.6 “Ordinal Number and Telling Time” (feminine ordinal hours, `الواحدة` for one, `الحادية عشرة`, `الثانية عشرة`, quarter/half/quarter-to); search summaries of OpenArabic/Noor Sisters (`والربع`, `والنصف`, `إلا ربعًا`, next hour after subtraction). Ordinals **validated** (NTS-08 final): LibreTexts 5.6 (1–12); M. Bussey after EMSA (11–19, 20–99, المئة, الألف) | Contract = indefinite short nominative without article; other ordinals above 99 fail closed; the thousands take the attached `و` connector (NTS-10) and the form their multiplier governs (NTS-14, see below). LibreTexts writes `وربع` / `إلا ربع`; the configuration keeps `والربع` / `إلا ربعًا` |
| HE | HE | none | Yes: adjectives 1–10, agreeing cardinal above | Declarative | Yes | 15 / 12 | gender (standalone/zachar/nekeva) | **Validated** (NTS-08 final): Academy of the Hebrew Language ("פרק אחד עשר…", dictionary שָׁעָה); University of Vermont (clock) | Above ten the cardinal is the intended ordinal, masculine by default also when a compound ends in a teen (NTS-14); thousands fixed by NTS-11 (see below); no ordinal of zero (NTS-12) |
| FA | FA, FA-IR | none | Yes | Declarative (`م`, `سوم`, `سی‌ام`) | Yes | 15 / 12 | none | **Validated** (NTS-08 final): MSU *Basic Persian*; Dehkhoda; UT Austin and Chai & Conversation (clock) | `اول` chosen for the standalone first; compounds use `یکم` |
| TR | TR, TR-TR | SCALE-SHORT | Yes | Declarative, every final word mapped (vowel harmony) | Yes | 15 / 12 | `case` (nominative/accusative/dative) | **Validated** (NTS-08 final): TDK Güncel Türkçe Sözlük and Yazım Kuralları "Sayıların yazılışı" | — |
| HI | HI | none | Yes (existing) | Declarative + `HindiOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | gender | **Validated** (NTS-08 final): Kamta Prasad Guru §175, §180; UNC *A Door Into Hindi*; HindiPod101 | Cardinals 21–99 lexicalized by NTS-10 (see below); nukta written in NFC (ढ + ़); NTS-08: Ordinals 0–99 999 |
| JA | JA | none | Yes (existing, prefix 第) | Declarative + `JapaneseOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | none | **Validated** (NTS-08 final): デジタル大辞泉 (第, 半) | No 午前/午後; NTS-08: Ordinals 0–1000 |
| KO | KO | none | Yes (existing, prefix 제) | Declarative + `KoreanOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | none | **Consulted**: National Institute of Korean Language (국립국어원), answer “몇 시, 몇 분으로 띄어 씁니다” (시 and 분 are dependent unit nouns); Hangeul Matchumbeop art. 43 (unit nouns spaced; attached `두시 삼십분` only allowed with numerals/order) | Native hour words are literal ClockTime patterns (all twelve tested); no 오전/오후; NTS-08: Ordinals 0–1000 (NTS-08) |
| ZH | ZH | none | Yes (existing, prefix 第) | Declarative + `ChineseOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | none | **Validated** (NTS-08 final): 汉典 (第, 两, 点, 刻); elon.io | `两` only for the displayed hour 2; quarter-hour step avoids single-digit minutes (`零五分`); no 上午/下午; NTS-08: Ordinals 0–100 and round hundreds |
| VN | VN, VI, VI-VN | none | Yes (repaired: `thứ tư`) | Declarative + `VietnameseOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | none | **Validated** (NTS-08 final): vtudien Việt-Việt (tư, thứ, giờ, rưỡi, kém) | No sáng/chiều/tối; NTS-08: Ordinals 0–999 |
| ID | ID, ID-ID | SCALE-SHORT | Yes | `IndonesianOrdinalLanguageSpecifics` (`long`) | Yes | 5 / 12 | none | KBBI entries `pertama`, `lewat`, `setengah`, `kurang` | Half hour refers to the following hour |
| MS | MS, MS-MY | ID | Yes | `MalayOrdinalLanguageSpecifics` (`long`) | Yes, replaces parent | 5 / 12 | none | DBP PRPM entries `lapan`, `pukul`, `suku`, `setengah`, `bilion`, `trilion` | Half hour keeps the current hour |
| CA | CA, ca-ES | none | Yes | Declarative | Yes | 15 / 12 | `gender=femení` forced on `{hour}` | IEC, *Gramàtica essencial*, §31.2; Optimot | Traditional quarters only; no ordinal of zero (NTS-14) |
| CA-valencia | ca-ES-valencia | CA | Inherited | Declarative | Yes, replaces parent | 5 / 12 | `una` forced; invariable `dos` in ClockTime only | AVL, *Gramàtica normativa valenciana*, §32.2; AVL glossary `dos` | `dos` limited to ConvertClockTime |
| EU | EU, eu-ES | none | Yes (existing) | Declarative + `BasqueOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | none | **Corrected** (NTS-08 final): Euskaltzaindia rules 7 and 18; Sareko Euskal Gramatika (clock) | Clock-case forms are literal per hour; cardinals unchanged; NTS-08: Ordinals 1–1000 (*bosgarren*); zero and > 1000 fail closed |
| SW | SW, SW-KE, SW-TZ | SCALE-SHORT | **Deferred** | — | Yes | 15 / 12, `hourOffset=-6` | none | **Consulted**: Five Colleges LangMedia, “Swahili – Tanzania – Telling Time”; SpokenSwahili, “Telling the time in Swahili” | No day-part words (asubuhi, mchana, jioni, usiku) |
| ZU | ZU | none | **Deferred** | — | Yes | 15 / 12 | none | **Validated** (NTS-08 final, two sources): Unisa *Learn online Zulu* Theme 4; DBE *IsiZulu Grade 4 and 5 Mathematics Terminology* | Hour forms are literal ClockTime patterns; they do not enable ordinals |
| EE | EE | SCALE-SHORT (scale tables only, NTS-23) | Yes (NTS-20): cardinal + `-lia` on the last element, `gbãtɔ` for 1 | Declarative + `ZeroOrdinalUnsupportedLanguageSpecifics` | **Deferred** | — | none | **Consulted** (NTS-20): Dzablu-Kumah, *Basic Ewe for Foreign Students*; Biblica Open Ewe Contemporary Scriptures; *Ewe Basic Course* 1968; Peace Corps Togo 2010; Wiktionary | Cardinals unbounded (NTS-23): static `akpe`, then the Conway-Wechsler short scale `miliɔn`, `biliɔn`, `triliɔn` (sourced anchors), `quadriliɔn` … (project extrapolation), each before its standalone multiplier (`multiplierPosition="afterScale"`); zero ordinal unsupported |
| WO | WO | none | Yes (`-éel`, NTS-16, NTS-19) | Declarative + `WolofOrdinalLanguageSpecifics` | **Deferred** | — | none | No consistent source found | Cardinals unbounded (NTS-21): static `junni`, then the long-scale Conway-Wechsler names `milyoŋ`, `milyaar` (attested anchors), `bilyoŋ`, `bilyaar` … (project extrapolation in Wolof spelling); ordinals 1–999 999 only, no ordinal of zero nor of the round thousands |

## NTS-30D — the Bulgarian conjunction *и* in compound cardinals (2026-10-14)

Scope: the place of *и* in every Bulgarian cardinal, through the generic `finalComponentConnector` primitive.
Old/new outputs and the regression dump: `DONE-2026-10-14(2).md`.

| # | Source | What it establishes |
|---|---|---|
| 1 | Речник на българския език (ИБЕ-БАН), entry *и* (conjunction), sense 3: "В съставни числителни имена (количествени и редни) свързва последното числително с предходното." Examples *Хиляда и петстотин години*, *сто тридесет и вторият* | the rule: *и* joins the **last** numeral to the preceding one, in cardinals and ordinals alike; *хиляда и петстотин* puts it between the groups when the last group is one numeral |
| 2 | Софийски университет, ФСлФ, ezik.bg "ПБЕ – ЮФ: Числително име": "Съставните числителни … се образуват чрез прибавяне, като се започва с най-голямата категория и се прибавят поотделно следващите до най-малката. Последната категория се прибавя със съюза и – *един милиард двеста милиона шейсет и пет хиляди седемстотин осемдесет и пет* (1 200 065 785)" | additive composition by categories, *и* before the last one; the model number has **two** *и*: one inside the multiplier of *хиляди*, one inside the last group, none between *милиард*, *милиона* and *хиляди* |
| 3 | БЕРОН (МОН), entry 1627 "Съставни числителни имена", after the Официален правописен речник 2012: *двайсет и пет, сто двайсет и пет, хиляда и триста* (read through a search-engine excerpt; the page itself answered 403) | within a group only before the last component (*сто двайсет и пет*, not *сто и двайсет и пет*); between groups before a one-numeral last group (*хиляда и триста*) |
| 4 | ИБЕ-БАН "Езикови справки", Г. Митринов, "За една особеност в употребата на числителните имена" (*Аз-буки*, бр. 13, 2020): *двайсет и петте, сто и деветте, триста двайсет и четирите* (already recorded by NTS-30B) | *сто и девет*, *триста двайсет и четири* |
| 5 | bulgaro.io, "Counting to 1000" (learner resource, not normative): "In bigger numbers, и is added before the last word of each group of three digits": *сто хиляди сто седемдесет и шест* (100 176), *сто и една хиляди сто седемдесет и шест* (101 176), *двеста и деветнадесет хиляди сто седемдесет и шест* (219 176) | corroborates #2 with compound multipliers before *хиляди* followed by a lower group: the multiplier keeps its own *и* |
| 6 | bg.wikipedia, "Имена на числата": *1024 – хиляда двадесет и четири (1000 + 20 + 4)*, *сто четиридесет и четири* | no *и* before a compound last group (*хиляда двадесет и четири*) |

**Answer to the audit question: local, not global.** The conjunction is selected independently in every numeral
phrase: each multiplier of a scale noun is a numeral of its own (*шейсет и пет хиляди*, *сто и една хиляди*,
*двадесет и една хиляди*), and the whole number is a phrase whose components are its categories (non-zero groups).
"Exactly one *и* in the whole string" is contradicted by #2 and #5. The rule implemented:

- inside a group, *и* precedes its last non-zero component, once (*двадесет и едно*, *сто и двадесет*, *сто двадесет и
  едно*, *двеста и пет*); 11–19 are single numerals (*сто и петнадесет*, never *сто пет и надесет*), as are the round
  tens and hundreds;
- between groups, *и* precedes the last non-zero group when that group is a single component (*хиляда и едно*,
  *хиляда и сто*, *хиляда и двеста*, *две хиляди и пет*, *десет хиляди и сто*, *двадесет и една хиляди и едно*), and
  nowhere when the last group already contains its own *и* (*хиляда сто двадесет и едно*, *сто хиляди сто седемдесет и
  шест*);
- a multiplier is built with its *и*, then takes the gender its noun governs (NTS-30B, NTS-30C), then the noun:
  *двадесет и една хиляди*, *двадесет и един милиона*, *двадесет и два трилиона* (never *двадесет една и хиляди*).

**Derived, not separately attested.** A last group that is itself a scale group follows the same category rule (#1,
#2): *един милион и сто хиляди* (1 100 000), *един милион и хиляда* (1 001 000), *един милион сто двадесет и една
хиляди* (1 121 000). No consulted source gives these exact values; no source suggests an exception for round
hundreds, round thousands, zero intervening groups or scale boundaries either.

**Spelling unchanged.** The converter keeps the full forms (*двадесет*, *шестдесет*) where #2–#4 use the colloquial
*двайсет*, *шейсет*, which the orthographic dictionary also lists; only the *и* changes.

**Ordinals unchanged.** `BulgarianOrdinalLanguageSpecifics` already applies the same local model (`JoinElements` per
multiplier, then *и* before a one-element last part: *двадесет и една хиляди и първи*, *хиляда сто и втори*). It
rebuilds its cardinal prefix itself because `IOrdinalLanguageSpecifics` receives no converter, so the cardinal change
does not make it redundant; its outputs are byte-identical in the regression dump.

## NTS-30C — generated scale nouns of Russian, Ukrainian, Bulgarian, Czech and Slovak (2026-10-14)

Scope: the agreement of every scale noun that `NumberScale` generates (from 10^12) with its multiplier, and the
multiplier gender a generated noun imposes. The rule is attached to the generated suffix family (one
`<GeneratedScaleForm suffix="…">` per `<Suffix>`), never to a scale index: the forms are suffix fragments joined to
the Conway prefix of each scale, so *триллион*, *квадриллион*, *дециллион* and *миллиниллион* share one entry. The
low scales 1–3 keep their exact `<ScaleForm>` (NTS-27, NTS-30B), which takes precedence. Old/new outputs and the
regression dump: `DONE-2026-10-14(1).md`.

| Language | Suffix family | Rule established | Sources | Implemented forms |
|---|---|---|---|---|
| RU | 0 (*-иллион*) | masculine noun declined like *миллион*: *триллион*, *два триллиона*, *пять триллионов*, *двадцать один триллион*, *двадцать два триллиона*; masculine multiplier (default digits) | ru.wiktionary *триллион* (declension table after Zaliznyak, *Грамматический словарь русского языка*: Р. ед. *триллиона*, Р. мн. *триллионов*), *квадриллион* (same root type); Unicode CLDR plural rules, ru | `RussianPluralLexicalFormSelector`; one *он*, few *она*, many *онов*; no forced variant |
| UK | 0 (*-ильйон*) | masculine noun of the 2nd declension like *мільйон*: *трильйон*, *два трильйони*, *п'ять трильйонів*; masculine multiplier | Горох, *трильйон* (словозміна: Н. мн. *трильйони*, Р. мн. *трильйонів*; чоловічий рід, 2 відміна); uk.wiktionary *трильйон* (*5 трильйонів доларів*); Unicode CLDR plural rules, uk | `UkrainianPluralLexicalFormSelector`; one *йон*, few *йони*, many *йонів*; the former suffix *йон(ів)* (no *few* form) became *йон* |
| BG | 0 (*-илион*) | masculine non-person noun like *милион*: singular after exactly one, count form (*бройна форма*) *трилиона* otherwise; masculine multiplier *един*, *два*, also in compounds (*двадесет и един трилиона*, with the *и* of NTS-30D) | Речник на българския език (rechnik.chitanka.info, *трилион*: м.р., бройна форма *трилиона*; *квадрилион*: *хиляда трилиона*); ИБЕ-БАН 2012, pp. 26–27 (count form after numerals) | default selector; singular *он*, plural *она*, both forcing `gender=masculine` on the multiplier |
| CS | 0 (*-ilion*) | masculine inanimate: *bilion*, *dva biliony*, *pět bilionů*; *jeden bilion* | IJP (ÚJČ AV ČR), entries *bilion*, *trilion*, *kvadrilion* (rod m. neživ.; 1. p. mn. *-iony*, 2. p. mn. *-ionů*; *kvadriliony hvězd*) | `CzechSlovakPluralLexicalFormSelector`; one *on* (forcing `gender=mužský`: *jeden*), few *ony*, other *onů* |
| CS | 1 (*-iliarda*) | feminine: *jedna biliarda*, *dvě biliardy*, *pět biliard* | IJP, entry *biliarda* (rod ž.; 1. p. mn. *biliardy*, 2. p. mn. *biliard*; *23 biliard dolarů*) | one *arda*, few *ardy* (both forcing `gender=ženský`: *jedna*, *dvě*), other *ard* |
| SK | 0 (*-ilión*) | masculine: *bilión*, *dva bilióny*, *päť biliónov* | KSSJ 4 (*bilión -a m. čísl. zákl.*); PSP 2000 (*sedem biliónov*) | `CzechSlovakPluralLexicalFormSelector`; one *ón*, few *óny*, other *ónov* |
| SK | 1 (*-iliarda*) | feminine like *miliarda*: *jedna biliarda*, *dve biliardy*, *päť biliárd* | KSSJ 4 / PSP 2013 (*miliarda -y -árd ž.*, NTS-30B); Slovak Wikipedia "Biliarda" (*biliarda* 10^15, *triliarda*, *kvadriliarda*, *kvintiliarda* …) | one *arda*, few *ardy* (both forcing `gender=ženský`: *jedna*, *dve*), other *árd* |

**Czech compounds before a feminine *-iarda* (22 × 10^15).** Decision: *dvacet dva biliard*, the genitive
construction with the counting form of the numeral, exactly as NTS-30B wrote *dvacet dva miliard*. Evidence: the IJP
("Počítaný předmět po číslovkách") allows *dvacet dva/tři/čtyři žáci* and *dvacet dva žáků* and calls the genitive
"more common and more natural"; in that construction the numeral does not agree with the noun, as shown by
*dvacet jedna žáků* (counting form *jedna* before a masculine noun) and by Turinská's *dvacet jedna krav*; the IJP
also writes the undeclined compound before a feminine noun with *dva* (*k tisíc sedm set dvacet dva korunám*,
"Členění čísel"). No source gives *dvacet dvě biliard*. Only the exact multipliers 1–4 take the nominative and agree
in gender (*dvě biliardy*). The NTS-30B note on *miliarda* is closed by the same evidence; scale 3 is unchanged.

**Slovak evidence level for *-iarda*.** No normative source consulted gives *biliarda* itself: KSSJ 4 lists only
*bilión* (and *biliard*, the game), and the JÚĽŠ portal could not be read. The series *biliarda*, *triliarda*,
*kvadriliarda* … is attested by the Slovak Wikipedia only, and its inflection (*biliardy*, genitive plural
*biliárd*) is extrapolated from the codified *miliarda -y -árd ž.* of the same productive family. Kept as a
documented reserve, not presented as normative.

**Slovak compounds.** A tens-with-units compound is one indeclinable word in its base form before any noun
(Jarošová 2021: *dvadsaťdva – pri všetkých substantívach základný tvar*), so *dvadsaťdva biliárd*, *dvadsaťjeden
biliónov*; only the exact 1–4 agree (*jedna biliarda*, *dve biliardy*).

**PL excluded.** PL declares `maxNumber="999999"`: no generated scale is in its public domain, so NTS-30C configures
none and does not change the guard. Opening it (Polish long-scale names, inflection, multiplier grammar) is NTS-35.

**HR not migrated.** HR spells its generated scales with scale-specific replacements (*milijun*, *milijarda*,
*bilijun*, *bilijarda*, *trilijun* …); the same families could move to `<GeneratedScaleForm>`, but its output is
outside NTS-30C and stays byte-identical.

**Conway spelling unchanged.** CS and SK still generate the Latin *quadri-*, *quinti-* prefixes (*quadrilion*,
*quintiliarda*), where the IJP writes *kvadrilion* and the Slovak series *kvadrilión*, *kvintiliarda*. NTS-30C
inflects these names correctly but does not respell them: NTS-36.

## NTS-30B — low scale nouns of Ukrainian, Bulgarian, Czech and Slovak (2026-10-14)

Scope: the agreement of the scales 1–3 (thousand, million, milliard) with their multiplier, and the multiplier
gender before them. Old/new outputs and the regression dump: `DONE-2026-10-14.md`. CLDR fixes only the selection
categories; the noun forms come from the dictionaries and grammars below.

| Language | Rule established | Sources | Implemented domain | Deliberate limitation |
|---|---|---|---|---|
| UK | *тисяча* (feminine), *мільйон*, *мільярд* (masculine) decline as nouns; nominative singular after …1 (not …11), nominative plural after …2–4 (not …12–14), genitive plural otherwise; the multiplier of *тисяча* ends in *одна*/*дві* (*двадцять одна тисяча*, *дві тисячі*, *три тисячі триста шістдесят чотири*, *п'ятсот п'ятдесят вісім тисяч*); 1000 is *тисяча* | Український правопис 2019, § 70.11 (pravopys.net); ХНЕУ, "Теоретичний матеріал" (числівник: *три тисячі триста шістдесят чотири*); Profpressa, "Узгодження числівника й іменника" (*два столи, три гривні; п'ять сторінок; п'ятсот п'ятдесят вісім тисяч гривень*); uk.wiktionary paradigms of *тисяча*, *мільйон*; Unicode CLDR plural rules, uk (= ru) | **Corrected**: `UkrainianPluralLexicalFormSelector`, `<ScaleForm>` 1–3, `onScale="1"` `EndsWith` replacements (*одна*, *дві*) | Generated names from *трильйон*: corrected by NTS-30C |
| BG | *хиляда* (feminine) takes *хиляди* after any multiplier but one; *милион*, *милиард* are masculine non-person nouns taking the count form (*бройна форма*) *милиона*, *милиарда* after a numeral, also after a compound ending in one (*хиляда и една нощи*, *двайсетте и един апарата*); masculine multiplier *един*, *два* | Официален правописен речник на българския език, ИБЕ-БАН 2012, pp. 26–27 (via bg.wikipedia "Бройна форма": *десет хиляди километра, един милион трактора, три милиона долара, пет милиарда лева*); ИБЕ-БАН "Езикови справки", "За една особеност в употребата на числителните имена" (*двайсет и петте, триста двайсет и четирите; двайсетте и един апарата*); `BulgarianOrdinalLanguageSpecifics` already writes *две хиляди*, *един милион*, *два милиона* | **Corrected**: `<ScaleForm>` 1–3 with the default singular/plural selector; `EndsWith` replacements *една* (scale 1), *един*/*два* (scales 2–3) | The missing conjunction *и* (*двадесет едно*, *двадесет един милиона*): corrected by NTS-30D (*двадесет и едно*, *двадесет и един милиона*). Generated names from *трилион*: corrected by NTS-30C |
| CS | *tisíc* (masc.), *milion* (masc.), *miliarda* (fem.): nominative after 1–4 (*dva tisíce, jeden milion, dva miliony, dvě miliardy*), genitive otherwise (*pět tisíc, pět milionů, pět miliard*); after a compound ending in 1–4 both are possible, the genitive being "more common and more natural" (*dvacet jedna žáků, dvacet dva žáků*), with the counting form *jedna* kept (*dvacet jedna krav*); *dvacet jedna tisíc* | Internetová jazyková příručka (ÚJČ AV ČR): "Počítaný předmět po číslovkách" (id 792), "Členění čísel, víceslovné číslovkové výrazy" (id 791: *dva tisíce (jedno) sto šedesát osm, dva tisíce dvě stě třicet jedna, jeden milion tři sta tisíc sedm set osmdesát tři*), entries *tisíc* (*dva tisíce pět set dvacet tři*), *milion*, *miliarda* (fem., *tři miliardy*, gen. pl. *miliard*); I. Turinská (FF UK, 2009), "Shoda u českých složených základních číslovek" (*dvacet jedna kráva / dvacet jedna krav; dvacet jedna tisíc i dvacet jeden tisíc*); Unicode CLDR plural rules, cs | **Corrected**: new `CzechSlovakPluralLexicalFormSelector` (one = 1, few = 2–4, other), `<ScaleForm>` 1–3, `onValue` replacements *jeden milion* and *dvě miliardy* | `dvacet dva miliard` keeps the counting form in the genitive construction (decided by NTS-30C on the IJP evidence). Generated names from *bilion* and the feminine *biliarda*: corrected by NTS-30C |
| SK | Cardinals are written as one word; in complex cases the thousands, the hundreds and the tens with units are written apart for readability (*sedemtisíc dvesto osemdesiattri*; *pätnásť miliónov tristoštyridsaťosemtisíc osemsto štyridsaťosem*); tens with units are always one word (*dvadsaťdva*), hundreds and thousands one word with their multiplier (*dvesto, tritisíc, dvadsaťsedemtisíc, stoosemdesiatdvatisíc*); 2000 is *dvetisíc* (indeclinable); *milióny*, *miliardy* apart and agreeing (*štyri milióny, päť miliónov, tri miliardy*); a tens-with-units compound is one indeclinable word governing the genitive plural (*dvadsaťdva žien*, *dvadsaťjeden* without gender) | Pravidlá slovenského pravopisu, 3rd ed. (JÚĽŠ SAV, Veda 2000), "Písanie slov osobitne a dovedna"; TASR, "Slovenčina: jednoslovné a viacslovné číslovky" (same rule); KSSJ 4 (2003), PSP 2013, OGS 2022: *dvetisíc neskl. čísl.*, *tisíc … v spoj. s počítaným predmetom obyč. neskl.*, *miliarda -y -iárd ž.*, *milión -a m.*, *dva … sto dva (častejšie stodva)*; A. Jarošová (JÚĽŠ SAV), "Zložené číslovky so zreteľom na písanie spolu a oddelene", *Slovenská reč* 86 (2021), no. 2, pp. 208–229, table 2.4.1 (*tisíc dvadsaťdva, tisíc sto dva, tritisíc tristo dvadsaťdva, päťstoštyridsaťtritisíc tristo dvadsaťšesť, päť miliónov dvestotridsaťštyritisíc päťsto šesťdesiatsedem*; *22 dvadsaťdva – pri všetkých substantívach základný tvar*, *dvadsaťdva žien*); Unicode CLDR plural rules, sk | **Corrected**: tens with units and hundreds fused in the default tables, a `<Groups onScale="1">` table fusing the whole multiplier of *tisíc*, `onScale="1"` replacements (*dvetisíc*, junction), `CzechSlovakPluralLexicalFormSelector` with `<ScaleForm>` 2–3, `onValue` replacements *jedna miliarda*, *dve miliardy* | The readable (spaced) variant is used, not the fully fused one (*sedemtisícdvestoosemdesiattri*; PSP allows both). 1 000 000 stays *jeden milión* (Jarošová writes *milión dvestotridsaťštyri…*; both in use, not changed). Generated names from *bilión*: corrected by NTS-30C |

**Slovak decision, *dvetisíc* vs *dva tisíce*.** The PSP rule for a basic (undeclined) cardinal is the one-word
series *dvetisíc, tritisíc, päťtisíc*; KSSJ, PSP 2013 and OGS 2022 list *dvetisíc* as an indeclinable cardinal, and
Jarošová (2021, note 20) states that a *dvetisíc*-type numeral is declined only when it does not determine a noun.
The noun-like *dva tisíce, päť tisícov* (KSSJ *tisíc*: plural *-e, -ov*) is the declined substantive use. A cardinal
spelled out by the library is the basic form, so *dvetisíc* is the default. The same rule writes the tens with units
as one word (*dvadsaťjeden*), which the thousands need (*dvadsaťsedemtisíc*), so the Slovak tens and hundreds were
fused in the same pass.

**Generated scales (NTS-30C).** Scale ≥ 4 samples after NTS-30B, all unchanged by it: UK *два трильйонів* (for
*два трильйони*), BG *две трилион* (for *два трилиона*) and *едно трилион*, CS *dva bilion* (for *dva biliony*) and
*dva biliard* (for *dvě biliardy*), SK *dva bilión* (for *dva bilióny*) and *dva biliard* (for *dve biliardy*), RU
*два триллион*. `ScaleForm` is indexed by an exact scale number; a generated name had no attached inflection rule.
All are corrected by NTS-30C (section above).

## NTS-30A — decimal fractions and decimal reading (2026-10-13)

Sources read during the pass (pages fetched on 2026-10-13). Old/new outputs: `DONE-2026-10-13(1).md`. Only French
and English were audited; every other language keeps its digit-by-digit reading, unaudited for named decimal
fractions and for a fallback limit.

### French

| Digits | Denominator | Source and the claim it supports | Status |
|---|---|---|---|
| 1–3 | *dixième*, *centième*, *millième* | OQLF, Banque de dépannage linguistique, "Écriture des fractions": *sept dixièmes (7/10)*, *trente-cinq millièmes (35/1000)*, *les trois centièmes (3/100)* | Already configured, unchanged |
| 4 | *dix-millième* | OQLF BDL, "Écriture des fractions": *quatre-vingts dix-millièmes (80/10 000)* | **Configured** `dix-millième(s)` |
| 5 | *cent-millième* | OQLF BDL, "Écriture des fractions": *cinq cent-millièmes (5/100 000)* | **Configured** `cent-millième(s)` |
| 6 | *millionième* | Dictionnaire de l'Académie française, 9e éd., *millionième*: "Qui équivaut à une unité d'un tout divisé en un million de parties égales", *Un millionième ou 1/1 000 000*, *Trois millionièmes* | **Configured** `millionième(s)` |
| 7, 8 | *dix-millionième*, *cent-millionième* | Rule, not attestation: a compound denominator is hyphenated (OQLF BDL: *trois-centième*, *dix-millièmes*, *cent-millièmes*; Académie, "« Vingt-cinq millièmes » ou « vingt cinq-millièmes » ?", 2022: the hyphen carries the value, *vingt cinq-millièmes* = 20/5000), applied to the attested *millionième* | **Configured** (productive rule on an attested base) |
| 9 | *milliardième* | Dictionnaire de l'Académie française, 9e éd., *milliardième*: "Qui équivaut à une unité d'un tout divisé en un milliard de parties égales", *Un milliardième* | **Configured** `milliardième(s)` |
| 10, 11 | *dix-milliardième*, *cent-milliardième* | Same hyphenation rule applied to the attested *milliardième* | **Configured** (productive rule on an attested base) |
| 12+ | (*billionième* …) | No Académie entry for *billionième* or higher (dictionary aggregators only) | **Not configured**: `LanguageDefault` reads 12+ digits one by one, `MathematicalFraction` throws |

Answers to the audit questions:

1. The denominator of 10^n is the ordinal noun of 10^n (*dixième* … *milliardième*), agreeing in number with the
   numerator (OQLF BDL: "le nom dénominateur s'accorde avec le nom numérateur").
2. A compound denominator is hyphenated (*dix-millième*, *cent-millième*); the numerator stays a separate cardinal
   (*quatre-vingts dix-millièmes*, *cinq cent-millièmes*), and the hyphen distinguishes *vingt-cinq millièmes* (25/1000)
   from *vingt cinq-millièmes* (20/5000).
3. Around the scale nouns the ordinal is formed on *million*/*milliard* (*millionième*, *milliardième*), with *dix-*/
   *cent-* hyphenated before it; no authoritative attestation was found from the billion (10^12) up.
4. The existing ordinal machinery cannot generate them: `ConvertOrdinal(10000)` writes `dix millième` (the FR
   configuration spells numerals without hyphens) and keeps the plural of a multiplied scale noun (`deux millionsième`,
   NTS-33).
5. Hence a static `<Fractions>` table, which is also what `ConvertFraction` uses for powers of ten.
6. Range configured: 1–11 digits.

Mixed numbers and reading modes:

| Point | Source and claim | Status |
|---|---|---|
| Integer + fraction joined by *et* | Éduscol (ministère de l'Éducation nationale), *Fractions et nombres décimaux au cycle 3*, p. 13: "2,4 se lira « deux et quatre dixièmes » plutôt que systématiquement « 2 virgule 4 »"; p. 3: *trois unités et dix-huit centièmes* (3,18). OQLF BDL, "Nom singulier après un nombre fractionnaire inférieur à deux": *un mètre et demi* | **Configured** `mixedFractionSeparator="et"` (*deux et un dix-millième*) |
| Digit-by-digit reading is in use | Same Éduscol page: *2 virgule 4* is a current formulation (discouraged in teaching, not wrong) | No fallback limit configured for FR |
| A pronounced digit 1 is *un* | Standalone cardinal (the level-1 text *et un* belongs to *vingt et un*) | `DigitByDigit` reads `zéro virgule zéro zéro zéro un` |

### English

| Point | Source and claim | Status |
|---|---|---|
| Denominators of 10^4, 10^5 | OpenStax, *Prealgebra 2e*, §5.1 "Decimals", place values: tenths, hundredths, thousandths, *ten-thousandths*, *hundred-thousandths* | **Configured** `ten-thousandth(s)`, `hundred-thousandth(s)`; 6+ digits not configured (millionths not checked in the source) |
| Integer + fraction joined by *and* | OpenStax §5.1, "Name a decimal number": "Write 'and' for the decimal point"; *5.24 = five and twenty-four hundredths*, *13.461 = thirteen and four hundred sixty-one thousandths*, *-2.074 = negative two and seventy-four thousandths* | **Configured** `mixedFractionSeparator="and"` |
| A zero integer part is not read | OpenStax §5.1: *0.05 = five hundredths* | `MathematicalFraction` reads the fraction alone |

### Pronounced digits in other languages

A digit read one by one is a standalone numeral, which differs from the level-1 composition text in three shipped
languages (checked over all 74 keys):

| Language | Pronounced digit | Composition text | Source | Obtained from |
|---|---|---|---|---|
| FR | *un* | *et un* (*vingt et un*) | standalone cardinal | existing `<Exceptions>` (`1 → un`), through the cardinal fragment |
| HU | *kettő* | *két* (attributive) | configuration comment "standalone 2 = kettő" | existing `<Exceptions>` (`2 → kettő`), through the cardinal fragment |
| DE | *eins* | *ein* (*einundzwanzig*) | PH Niederösterreich, G. Egger, *Dezimalzahlen* (2022): "Wie liest man Dezimalzahlen? … 5,01 „fünf Komma null eins“" | `<DecimalDigits><DecimalDigit digit="1" string="eins" />`: the fragment is *ein*, the standalone *eins* was only produced by the German finalizer, which must run once on the whole phrase |

No source was consulted for the zero word of the other languages: each reads its configured `zero` (NTS-34).

## NTS-31 — Dutch long-scale cardinals (2026-10-11)

Sources read during the pass. Old/new outputs: `DONE-2026-10-11(2).md`. Under the repository's Conway rule
(`AGENTS.md`, root `README.md`), the audit establishes the Dutch base pattern and its orthography; the higher names
are the productive Conway-Guy-Wechsler continuation of that pattern.

| Point | Rule established | Sources | Status |
|---|---|---|---|
| Long scale | -iljoen every factor one million, -iljard in between: *een miljoen miljoen is een biljoen; een miljoen biljoen is een triljoen*, *duizend miljoen is een miljard; duizend biljoen is een biljard* | Onze Taal 70 (2001), "Een miljard of een biljoen?" (DBNL) | **Attested base** |
| Names to the centiljoen | *miljoen, miljard, biljoen, biljard, triljoen, triljard, quadriljoen, quadriljard, quintiljoen … octiljoen, noniljoen, deciljoen, vigintiljoen, centiljoen* | nl.wikipedia "Lijst van machten van tien" (secondary) | **Attested base**: Latin *quadri/quinti/sexti/octi* (not the Swedish *kvadri/kvinti/okti*) |
| 10^6000 (competing conventions) | The same traditional/secondary Dutch list gives *milliljoen* at 10^6000. This diverges from the strict Conway-Guy-Wechsler continuation selected by the library, which gives *milliniljoen* at that boundary (English *millinillion*: the 1000th index is *milli* + empty group *ni*). | nl.wikipedia "Lijst van machten van tien" (secondary); Conway-Guy-Wechsler | **Recorded, not used**: the traditional *milliljoen* is source evidence of a different extension system and does not override the project's productive Conway system (`AGENTS.md`); *milliniljoen* is generated |
| Scale nouns separate, singular after the numeral, no *en* | "Miljoen en miljard zijn altijd aparte woorden": *drie miljoen*, *vijf miljard tweehonderd miljoen*, *driehonderdzevenenveertig miljard zeshonderdvijfentwintig miljoen zevenhonderdachtentwintigduizend tweehonderdeenentwintig* | Taaladvies.net (Taalunie), "Aaneenschrijven van telwoorden (algemeen)" | **Implemented**: invariant suffixes `joen`/`jard`, space between groups |
| *een* before miljoen | *een miljoen*, *een biljoen* (unlike *duizend*, which drops it: existing NTS-08 replacement) | Onze Taal 70 (2001) | **Implemented** (no replacement added) |
| Junction | *mi + l + joen* (*miljoen*, *biljard*) before the suffix; the inherited strict Conway joint *-illi-* between the groups of an index from 1000 (*milli + ni + l + joen* → *milliniljoen*). `groupSeparator="l"` would give *milniljoen*, which is neither the traditional *milliljoen* nor the Conway form | derived from the attested names; Conway-Guy-Wechsler | **Implemented**: `suffixSeparator="l"`, `groupSeparator` inherited |
| Higher names | *undeciljoen*, *septendeciljoen*, *trigintacentiljoen*, *milliniljoen* … with the strict NTS-24 tables | Conway-Guy-Wechsler (inherited SCALE-LONG) | **Productive project extension**, not individually attested |

### NTS-31 — Dutch ordinals from a million (2026-10-13)

Old/new outputs and the exact changed set: `DONE-2026-10-13.md`. The ordinal keeps the written structure of the
cardinal and only its final element becomes ordinal; no other part is ordinalized, so the Spanish-style
`<OrdinalComposition>` (ordinal head + ordinal tail) does not apply.

| Point | Rule established | Sources | Status |
|---|---|---|---|
| Round scale ordinals | The scale nouns take *-ste*: *miljoen → miljoenste*, *miljard → miljardste*; productive on every *-joen*/*-jard* noun (*biljoenste*, *biljardste*, *triljoenste*). Derived ordinals keep the spelling structure of the cardinal and ordinalize its final element | e-ANS (Algemene Nederlandse Spraakkunst), rangtelwoorden | **Implemented**: stems `joen → joenst`, `jard → jardst` + the existing suffix `e` |
| Multiplier one | A round value with the multiplier one is the scale noun alone: *miljoenste*, *miljardste*, not *een miljoenste* | e-ANS (scale ordinals given without *een*) | **Implemented**: `<OrdinalScale scales="2..6">` (miljoen … triljoen, the highest scale a `long` reaches); *duizend* keeps the cardinal path, which already drops *een* (*duizendste*) |
| Multiplier above one | The multiplier stays a separate cardinal word, followed by a space: *de twee miljoenste*, not *tweemiljoenste* | Besluit bekendmaking spellingvoorschriften 2005, rule 6.N | **Implemented**: `multiplierSeparator=" "` (*twee miljoenste*, *eenentwintig miljoenste*, *honderd miljoenste*, *twee miljardste*) |
| Compound scale ordinals | The earlier groups stay cardinal, only the final element becomes ordinal, no *en* between the groups: *de twee miljoen tweehonderdduizendste*, *de twee miljoen tweehonderdduizend tweehonderdtweeëntwintigste* | Besluit bekendmaking spellingvoorschriften 2005, rule 6.N | **Implemented** by the usual last-word path (*een miljoen eerste*, *een miljoen duizendste*, *twee miljard een miljoenste*) |
| Domain | Productive rule over the whole cardinal domain | — | `DutchOrdinalLanguageSpecifics` widened to the magnitudes 0–`long.MaxValue` (rejects nothing), i.e. the signed domain `-long.MaxValue`..`long.MaxValue`; `long.MinValue` (absolute value not representable) and values beyond `long` are rejected by the engine contract, as in every language |

## NTS-29 and NTS-32 — attested zero ordinals and the Polish 400th (2026-10-11)

Sources read in a browser during the pass. Old/new outputs and the exact changed set: `DONE-2026-10-11(1).md`.

| Lang. | Rule established | Sources | Status |
|---|---|---|---|
| DA | The ordinal of zero is *nulte* ("talord (ordenstal)", "som er nr. 0 i en rækkefølge", "I nulte C på Katrinebjergskolen i Århus") | Den Danske Ordbog, *nulte*; Retskrivningsordbogen since 1986 (Dansk Sprognævn SV00001178) | **Implemented** by a Danish-only override of the protected `ZeroOrdinal` hook of `ScandinavianOrdinalLanguageSpecifics` (default `null`); NO and SV still fail closed for zero |
| HU | "0." written in letters is *nulladik*; the cardinal stays *nulla* | MTA Nyelvtudományi Intézet, helyesiras.mta.hu "Számok" [AkH11-290, AkH12-292; AkH12-291a] | **Implemented** in `HungarianOrdinalLanguageSpecifics` (negative values still declined by the plugin) |
| PL | The ordinal of 400 is *czterechsetny* (syllabified *czterech•set•ny*), declined as a regular -ny adjective in every gender and case (*czterechsetna rocznica*, *czterechsetną rocznicę*); *czterosetny* has no entry | Wielki słownik języka polskiego PAN, hasło 39657 *czterechsetny* (definition and full "Odmiana" table); SJP PWN *czterechsetny* (KJP examples), search *czterosetny* "Nie znaleziono"; PWN Poradnia 16446 (*osiem tysięcy czterechsetny*) | **Corrected**: the 30 forms of the plugin's 400 row and the reference word rule of the XML; only ordinals whose last component is the round 400 change (400, 1400, 2400 …); 401 *czterysta pierwszy*, 4000 *czterotysięczny* unchanged |

## NTS-28 — compound and large-scale ordinals (2026-10-11)

Every family recorded by NTS-28 was re-audited against sources read during the pass (RAE, Ciberdúvidas, RAG,
greek-language.gr, PWN, slovnyk.ua in a browser; PDFs extracted locally). Domains, old/new outputs and final
dispositions: `DONE-2026-10-11.md`. The guards still bound every unsourced family.

| Language | Rule established | Sources read | Result |
|---|---|---|---|
| ES | Complex ordinals juxtapose the simple ones from the highest order (*trigésimo primero*, *centésimo trigésimo cuarto*, *milésimo ducentésimo cuadragésimo octavo*), each varying in gender when written in two words; simple round hundreds (*ducentésimo* … *noningentésimo*); thousands and millions prefix the multiplying cardinal (*dosmilésimo*, *diezmilésimo*, *cienmilésimo*, *quinientosmilésimo*, *millonésimo*, *milmillonésimo*, *tresmilésimo tricentésimo cuadragésimo quinto*) | RAE/ASALE, DPD "ordinales" §1 (table), §2a–d, §3 | **Implemented** 1–999 999 999; multipliers not shown (ending in *uno*, several words, feminine *-cientas*) fail closed |
| PT | Round hundreds *ducentésimo, trecentésimo (tricentésimo), quadringentésimo, quingentésimo, seiscentésimo (sexcentésimo), septingentésimo, octingentésimo, nongentésimo (noningentésimo)*; compounds juxtapose agreeing ordinals (*duas milésima trecentésima quadragésima quinta*); 2000 disputed (*dois milésimo*, *dois milésimos*, *segundo milésimo*; *décimo milésimo*) | Ciberdúvidas 16428 and 29849 (citing E. Bechara, *Gramática Moderna do Português*, 2002, pp. 208–209; P. Teyssier) | **Implemented** 1–1999 (first form given); ≥ 2000 fail closed |
| GL | *vixésimo primeiro, vixésimo segundo, etc.*; gender and number "nas formas compostas só se unen ao último elemento" (*décimo primeira*); simple *centésimo, milésimo, millonésimo* | RAG/ILG NOMIG (2003) §16.2 (PDF p. 77); Dicionario da RAG: *ducentésimo, tricentésimo, cuadrinxentésimo, quinxentésimo, sexcentésimo, septinxentésimo, octinxentésimo, noninxentésimo* | **Implemented** 1–1999 (ordinal-only replacements, last element agrees); ≥ 2000 fail closed |
| EL | Compounds of agreeing ordinals (*εικοστός*: "Tην εικοστή τρίτη του μηνός"; *εκατοστός*: "Η εκατοστή πρώτη μέρα"); *δισχιλιοστός* is not an entry | Λεξικό της Κοινής Νεοελληνικής (greek-language.gr) | **Implemented** 1–1999; ≥ 2000 fail closed |
| FI | Ordinal marker in every part, one word (*viideskymmenesviides*); *kahdeskymmenesensimmäinen* more common than *…yhdes*; 11–19 marked on the first part; inflected compounds inflect every part (*kolmannenkymmenennenkuudennen*) | Kielitoimiston ohjepankki "Luvut ja numerot: järjestysluvut" and "… järjestyslukujen taivuttaminen"; Aalto/Kielenhuollon käsikirja paradigms (1, 3, 5, 17, 20, 100, 1000) | **Implemented** nominative 0–1999; other cases and ≥ 2000 fail closed (cardinal `kaksi tuhat`, NTS-30) |
| PL | Only tens and units ordinal (*dwa tysiące piętnasty*, *osiemnaście tysięcy sto czterdziesty drugi*, *tysiąc pięćsetny*); round thousands one word (*dziewiętnastotysięczny*, *dwudziestojedentysięczny*, *dwudziestodwutysięczny*); multipliers from 100 decomposed unexpectedly (*sto tysięcy tysięczny*) | PWN Poradnia 16446 (K. Kłosińska, 2015); SJP PWN *tysiąc* | **Implemented** 1–999 999 except round ≥ 100 000. The same answer's *czterechsetny* differed from the plugin's *czterosetny* (NTS-32, corrected 2026-10-11) |
| RU | §132б one word with *-сотый, -тысячный, -миллионный* (*пятнадцатитысячный*, *шестисотмиллионный*); §133б composite ordinals separate, only the last part ordinal (*две тысячи первый*, *миллион двести тысяч сто пятый*) | Правила русской орфографии и пунктуации (2006), PDF orfo.ruslang.ru, p. 139 | **Implemented** 0–999 999 999 (round multipliers ending in *один* or 100–199 fail closed) |
| UK | §38.1.2: *двохтисячний, десятитисячний, п'ятсоттридцятитисячний; чотирьохмільйонний, п'ятдесятимільйонний, шістдесятип'ятимільйонний; семимільярдний, трьохмільярдний*; note 1: *мільйон трьохтисячний* | Український правопис 2019 §38 (slovnyk.ua) | **Implemented** round multipliers 1–89 (not …1 after the tens); ≥ 90 and hundreds fail closed |
| SV | — | SAOL/Språkrådet searched; nothing settles *två miljonte* / *tvåmiljonte* nor *miljardte* | **Deliberately fail-closed** above a million (unchanged) |
| AR | — | Contract is the indefinite short form; the only form above 100 found (*المئة والسابع والثلاثون*) is definite | **Deliberately fail-closed** above 100 (unchanged) |
| NL | Long scale (*duizend miljoen is een miljard*, *duizend miljard een biljoen*) | Onze Taal "Een miljard of een biljoen?" (DBNL); webwoordenboek.nl (secondary) | **Blocked by NTS-31** (the cardinal stopped at 999 999; cardinal part closed 2026-10-11, ordinals from a million closed 2026-10-13) |
| JA | 第 "数を表す語に付いて、ものの順序を表す" (any numeral) | デジタル大辞泉 (NTS-08) | **Widened** to the cardinal domain (10^16 − 1) |
| KO | 제 + Sino-Korean numeral (제삼 항) | 한글 맞춤법 제43항 (NTS-08) | **Widened** to the cardinal domain (10^16 − 1) |
| ZH | 第 "用於整數數詞之前" (any integer numeral) | 國語辭典 via 汉典 (NTS-08) | **Widened** to the cardinal domain (10^12 − 1) |
| VN | thứ "Đặt trước số từ để chỉ thứ tự" | vtudien Việt-Việt | **Widened** to the cardinal domain, except a whole zero class (NTS-30) |
| HI | §180 (इ) "सौ से ऊपर की संख्याओं में पिछले शब्द के अंत में वाँ लगाते हैं" | Kamta Prasad Guru, हिंदी व्याकरण (hi.wikisource) | **Widened** to the cardinal domain (एक लाखवाँ, एक करोड़वाँ) |
| EU | Ordinal = cardinal in letters + *-garren* ("… ehungarren, milagarren …"), *batgarren* separate | Euskaltzaindia rule 18 (PDF) | **Widened** to 1–999 999 999 (≥ 10^9 NTS-30) |

## NTS-27 — cardinal corrections (2026-10-10)

The cardinal defects that NTS-08 recorded (NTS-27) were corrected against the sources below, read during the
pass (RAE, PWN, Isof pages in a browser; nothing behind a CAPTCHA). The ordinal guards of the final validation
are unchanged; the cardinals no longer block them (NTS-28). Details and old/new outputs: `DONE-2026-10-10.md`.

| Language | Rule established | Sources read | Status |
|---|---|---|---|
| SV | Numbers below a million are one word (*tjugofem*, *femtusentrettio*, *trehundranittiofemtusen*); *miljon*, *miljard* stand apart (*tjugosex miljarder femhundraåttiosju miljoner*); after the tens *en* before a counted word (*tjugoen kilo*, *etthundratjugoen tallrikar*), *tjugoett* for the number itself; three identical consonants: one dropped | Språkrådet (Isof), Frågelådan 27138, 21216, 31080 | **Corrected**: `etthundraett`, `tvåtusen`, `tjugoentusen`, `etthundraettusen`, `en miljon`, `tjugoen miljoner`. Choice: *etthundra* (attested in 21216) rather than *hundra*; *tusen* for 1000 kept |
| PL | *tysiąc* after one alone, *tysiące* after 2–4 (not 12–14, also at the end of a compound), *tysięcy* otherwise | SJP PWN *tysiąc* (*trzy tysiące, sześć tysięcy*); PWN Poradnia 16446 (*osiemnaście tysięcy*, *pięć milionów czterysta dwadzieścia sześć tysięcy*); Unicode CLDR plural rules, pl | **Corrected** (`PolishPluralLexicalFormSelector`) |
| RU | *тысяча* feminine (*одна тысяча*, *две тысячи*); *тысяча/миллион/миллиард*: singular after …1 (not …11), genitive singular after …2–4 (not …12–14), genitive plural otherwise | Unicode CLDR plural rules, ru; ru.wiktionary *тысяча* | **Corrected** (`RussianPluralLexicalFormSelector`, scales 1–3); generated names from *триллион* not agreed (NTS-30) |
| VN | 1000 is *một nghìn*; a class after a higher class is read with its zero hundreds (*không trăm*) and *linh* before a lone unit | SGK Toán 4 solution keys: vietjack.com (p. 15 ex. 2, *bốn trăm triệu không trăm bảy … nghìn một trăm chín mươi hai*), loigiaihay.com (*… nghìn không trăm linh năm*) | **Corrected**. Pedagogical sources only (no normative body publishes number reading); a whole zero class read *không nghìn* in the same keys is not built (NTS-30) |
| JA | Groups of four digits with 万, 億, 兆; 一 not said before 百, 十, optional before 千 (kept: 千); no spaces | ja.wikipedia *漢数字* (命数法 section) | **Corrected**; to 9999兆 |
| KO | Counting by 만, 억, 조, spaced by units of 만 only (*십이억 삼천사백오십육만 칠천팔백구십팔*); 일 not written before 십, 백, 천; written before 만/억/조 (*일억 삼천만*) | 한글 맞춤법 제44항 via 국립국어원 새국어소식 (2004) | **Corrected**; to 9999조. The multiplier one of 만 (*일만* vs colloquial *만*) is a documented choice |
| ZH | Counting by 万, 亿; one 零 for a run of skipped positions, none at the end of a group; 十 keeps 一 except at the head (一百一十) | AllSet Learning, Chinese Grammar Wiki, *Zeros in numbers* | **Corrected**; below 万亿 (10^12), which needs a multiplier counted by 万 itself. `两` stays a ClockTime literal |
| HI | हजार 10^3, लाख 10^5, करोड़ 10^7, अरब 10^9, खरब 10^11; the higher names (नील, पद्म, शंख) "बहुधा असंख्यता का बोध"; spelling पाँच | Kamta Prasad Guru, *हिंदी व्याकरण* §173, §176, §180 (hi.wikisource, chapter विशेषण) | **Corrected** (`groupPattern="3,2"`, domain below 100 खरब, पाँच everywhere) |
| EU | *mila eta berrehun* but *mila berrehun eta bi* (the *eta* after the thousands is dropped when the following hundreds carry their own); *milioi bat*, *hamar milioi*, *mila milioi*; 600 *seiehun* | Euskaltzaindia, rule 7 *Zenbakien idazkeraz* (1994), table and notes | **Corrected**. The *eta* is applied from the thousand upward also after *milioi* (*milioi bat eta bat*), as the note's "Mila zenbakitik gora" states; *seirehun* → *seiehun* also corrects the ordinals 600–699 |
| ES | Apocope of *uno* and its compounds before *mil* and the nouns *millón/millones* (*veintiún mil*, *treinta y un mil*, *ciento un mil*, *un millón*); *mil* not *un mil*; *mil millones*; *ciento uno* in two words | RAE/ASALE DPD *cardinales* (table, §2, §3, §4), *uno* §2.2–2.3, *mil* §1 | **Corrected**; feminine before *mil* keeps the apocope (allowed by §3); *mil millones* + millions not composed (NTS-30) |

## NTS-08 final validation (2026-10-09)

Every ordinal and ClockTime capability still open in `TODO.md` was audited against sources read during
this pass (pages fetched and the relevant passage read; pages behind Cloudflare or a JavaScript wall were
read in a browser; nothing protected by a CAPTCHA was bypassed). Each capability is now **Validated**
(an authoritative normative or academic source, or two independent reliable sources), **Corrected**
(the source contradicted the output; the `.feature` expectation was changed first, run red, then the
configuration or plugin was fixed) or **Deferred / fail-closed** (the value is rejected with
`NotSupportedException` because no source establishes its form or the form produced was wrong and cannot be
built by the existing machinery). The sweeping per-language guards are in
`OrdinalDomainGuardLanguageSpecifics.cs` (one shared base class, one sealed class per language).

### Ordinals

| Language | Status | Domain kept | Sources read | Notes |
|---|---|---|---|---|
| SV | **Corrected** | 1–1 000 000 | SAOB (TJUGO: compounds with *förste … nionde*; TJUGONDE, HUNDRADE, TUSENDE; MILJONTE "använt ss. ordningstal"); Språkrådet, Frågelådan 27138 (one word below a million; *miljon*, *miljard* apart), 31080 (three identical consonants: one dropped), 21216 (*en* after the tens) | `tjugoetttusende` → `tjugoentusende`, `hundraetttusende` → `hundraettusende`; above a million fail closed (no source for *två miljonte* vs *tvåmiljonte*; SAOB has no *miljardte*) |
| DA | **Validated** | `int` | Dansk Sprognævn SV00000084 (*fyrretyvende* … *halvfemsindstyvende* in Retskrivningsordbogen, short forms equally correct), SV00000047, SV00017853 (*et hundrede og en*, *en million og en*); DSL, "Den 101. dalmatiner" (*hundrede*, split ordinals above 100); Den Danske Ordbog (*tusinde²*, *millionte*, *milliardte* ordenstal); Systime, *Sproglig Matematik* (Tyvende, Tredivte …) | Ordinal of zero *nulte* is in Retskrivningsordbogen (SV00001178); enabled by NTS-29 (2026-10-11, Den Danske Ordbog) |
| HU | **Corrected** | `int` | AkH12 §291 (MTA); MTA Nyelvtudományi Intézet "Számok" tool (AkH12-292), compared on 60 values | `milliomodik` → `egymilliomodik`, `milliárdodik` → `egymilliárdodik`, `millió-egyedik` → `egymillió-egyedik`; every other value equal to the tool. Zero *nulladik* enabled by NTS-29 (2026-10-11) |
| UK | **Corrected** | unchanged (≤ 10 000 round thousands, millions/milliards of one) | Український правопис 2019, §38 (one-word *-сотий/-тисячний/-мільйонний*; *мільйон трьохтисячний*, *мільярд чотирьохмільйонний*); goroh.pp.ua paradigms (VESUM data) for перший, третій, тисячний | `один мільйон перший` → `мільйон перший`. The §38 rule would license more round values (*п'ятсоттридцятитисячний*, *чотирьохмільйонний*); not built (NTS-28) |
| FA | **Validated** | `int` | Michigan State University, *Basic Persian* §1.8 (اول standalone, بیست و یکم, سیُم, صدم, هزارم, یک میلیونم); Dehkhoda (یکم, سی‌ام, هزارم); Wiktionary ـُم (secondary) | Standalone اول / compound یکم confirmed; zero صفرم kept (productive rule + *قانون صفرم ترمودینامیک*) |
| TR | **Validated** | `long` | TDK Güncel Türkçe Sözlük: *sıra sayı sıfatı* "-ıncı ekiyle", entries birinci … bininci; TDK Yazım Kuralları "Sayıların yazılışı" rule 2 | milyonuncu, milyarıncı by the stated rule; zero *sıfırıncı* (productive; *sıfırıncı yasa*) |
| AR | **Validated** | 1–99, 100, 1000 | LibreTexts, *Introduction to Arabic II* 5.6 (1–12 m/f); M. Bussey, "All the Arabic …", Arabic Numbers (after EMSA): 11–19, tens = cardinal, *الحادي والعشرون*, indefinite *ثانٍ*, المئة, الألف | Productive *المئة والسابع والثلاثون* exists but is not enabled |
| HE | **Validated** | 1–999 999 | Academy of the Hebrew Language, "פרק אחד עשר או פרק אחת עשרה?" (ordinal above ten = agreeing number: פרק אחד עשר, פרק עשרים ושלושה, הפרק השלושים וחמישה) | The סתמי alternative (always feminine) is not an ordinal and is not modelled |
| VN | **Validated + fail-closed** | 0–999 | vtudien Việt-Việt: *tư* "xếp thứ tư", *thứ* "hàng thứ nhất" | From 1000 the cardinal is wrong (`nghìn`, `hai nghìn năm`): closed (NTS-27) |
| CS | **Validated** | unchanged | IJP (ÚJČ AV ČR) id 785, 791 (*dvacátý pátý, stý padesátý, čtyřstý sedmdesátý osmý, tisící devítistý padesátý šestý*), slovník (*jednadvacátý* lze i *dvacátý první*; *dvoustý první*; *tisící dvoustý padesátý druhý*; *miliontý*, *miliardtý*); cs.wiktionary paradigms of *druhý*, *třetí* | Supersedes the search summary of Naše řeč; masculine accusative is the inanimate form |
| SK | **Validated** | unchanged | JÚĽŠ SAV (KSSJ 2003, PSP 2013, OGS 2022): *tretí -tia -tie*, *piaty -ta -te*, *tisíci -ca -ce*, *dvojstý* (*dvestý → správ. dvojstý*), *dvadsiaty tretí/tretia/tretie* | Declension tables (long, short, soft) match |
| NL | **Corrected** | 0–999 999 | Taaladvies (Taalunie), "Aaneenschrijven van telwoorden (algemeen)"; Woordenlijst Nederlandse Taal (*nulde*, *tweeëntwintigste*, *tweeduizendste*, *honderdeerste*) | Cardinal fixed because it made the ordinals wrong: one word up to *duizend*, space after it, diaeresis; ordinals by ending (suffix `e` + `OrdinalStem`); from a million the ordinal (previously an `InvalidOperationException` from the scale lookup) fails closed through `DutchOrdinalLanguageSpecifics` (PR #629 review); widened to `-long.MaxValue`..`long.MaxValue` by NTS-31 (2026-10-13) |
| PL | **Corrected + fail-closed** | 1–1999 | ZPE (MEN), "Odmiana liczebnika i zaimka" (table of *drugi*; *tysiąc pięćset dwudziesty piąty*; *tysiąc osiemsetny*; *tysięczny*; *dwa tysiące pierwszy*) | 1000–1999 rebuilt (*tysiąc dziewięćset dziewięćdziesiąty dziewiąty*, *tysięczna*); from 2000 closed (NTS-27, NTS-28); 400 *czterosetny* → *czterechsetny* (NTS-32, WSJP PAN) |
| RU | **Validated + fail-closed** | 0–1999 | Правила русской орфографии и пунктуации (2006), §132–133 (gramota.ru) | From 2000 closed (`два тысячный`, `один миллионый`; NTS-27, NTS-28) |
| ES | **Corrected + fail-closed** | 1–29, round tens, 100, 1000 | RAE/ASALE, DPD "ordinales" (full table, univerbal second decade, accent, gender on the last component) | Cardinal accent 22/23/26 fixed (*veintidós*) so the word rules apply; *vigesimoséptimo*, *vigesimoctavo*, feminine *vigesimotercera* …; from 31 closed (NTS-28) |
| PT | **Validated + fail-closed** | 1–20, round tens, 100, 1000 | Priberam (*vigésimo* "depois do décimo nono", *septuagésimo nono*, *octogésimo nono*, *centésimo*, *milésimo*) | `vinte e primeiro`, `duzentos` closed (NTS-28) |
| GL | **Corrected + fail-closed** | 1–20, round tens, 100, 1000 | RAG/ILG, NOMIG (2003) §16.2; RAG dictionary (*cuadraxésimo*) | `trece` → `décimo terceiro`, `cuadragésimo` → `cuadraxésimo` (…octoxésimo), feminine on the last element (*décimo terceira*) |
| EL | **Corrected + fail-closed** | 1–20, round tens, round hundreds, 1000 | Λεξικό της Κοινής Νεοελληνικής (εικοστός, δέκατος, εκατοστός "Η εκατοστή πρώτη μέρα", διακοσιοστός, χιλιοστός) | Feminine/neuter 13–19 (*δέκατη τρίτη*); `είκοσι πρώτος` closed (NTS-28) |
| FI | **Validated + fail-closed** | nominative of 0–20, round tens, round hundreds, 1000 | Aalto University, "Numeroiden taivutus" (nominative/genitive/partitive of ensimmäinen, seitsemästoista, kahdeskymmenes, sadas, tuhannes); en.wikipedia *Finnish numerals* (kahdeskymmenesensimmäinen) | Inflected cases were the cardinal forms (`kolmen` for *kolmannen*): closed (NTS-28) |
| HI | **Validated + fail-closed** | 0–99 999 | Kamta Prasad Guru, *हिंदी व्याकरण* §180 (hi.wikisource) | From 100 000 the cardinal is wrong (`एक सौ हज़ार`): closed (NTS-27) |
| JA | **Validated + fail-closed** | 0–1000 | デジタル大辞泉 / 日本国語大辞典 "第" (kotobank) | Above 1000 the cardinal is wrong (`千 一`, `十 千`): closed (NTS-27) |
| KO | **Validated + fail-closed** | 0–1000 | 한글 맞춤법 제43항 as summarized by the Supreme Court library (scourt.go.kr): *제삼 항*, numbers spaced by 만 | Above 1000 the cardinal spaces the thousands: closed (NTS-27) |
| ZH | **Validated + fail-closed** | 0–100, round hundreds | 國語辭典 via 汉典 "第" | 101–999 non-round and ≥ 1000 rely on a cardinal without 零/一十: closed (NTS-27) |
| EU | **Corrected + fail-closed** | 1–1000 | Euskaltzaindia rule 18 (ordinal spelling) and rule 7 (cardinals) | `bostgarren` → `bosgarren`; zero (`zerogarren`, unattested) and > 1000 (`mila bat`, `bat milioi` against the rule) closed (NTS-27) |

### ClockTime

| Language | Status | Sources read |
|---|---|---|
| DA | **Validated** | Den Danske Ordbog *kvart¹* ("kvart i syv", "kvart over elleve"), *halv* 1.c ("klokken var halv fire"); Langenscheidt DA-DE (*klokken er ét*, *ti minutter i/over otte*); elon.io (*fem minutter i halv ti*) |
| NO | **Validated** | Bokmålsordboka (UiB/Språkrådet): *halv* "30 minutter før en hel time", *kvart* "kvart over ett", "kvart på fire", *over*; en.wikipedia *Date and time notation in Norway*, NLS (*ti på halv fire*, *fem over halv sju*) |
| SV | **Validated** | SAOB *kvart* 6 ("kvart över tolv", "kvart i tolv"), *halv* γ ("halv sex"), β ("fem minuter i/över halv"); Språkrådet 22011, 21216 (*ett* for one o'clock) |
| BG | **Validated** | Речник на българския език (ИБЕ БАН): *петнайсет* (съкр. of петнадесет), *четвърт* ("Осем часа без четвърт"), *половина* (… час и съюз *и*), *без* 6 |
| HR | **Validated (weakest; documented decision)** | Školski rječnik hrvatskoga jezika (IHJJ): *sat*, G *sata*, mn. *sati*; lets-learn.eu "Time in Croatian" (*pola deset*, *petnaest do devet*, *tri (sata) i petnaest*). No normative clock rule exists in IHJJ/HJP; a Croaticum textbook could not be read legally and a Masaryk article sits behind a CAPTCHA. The single pedagogical source is retained because it contradicts nothing and the morphology is dictionary-backed |
| HU | **Validated** | Giay & Nádor, *Magyar mint idegen nyelv – Hungarológia* (1998, MEK), base points *negyed, fél, háromnegyed* + next hour |
| CS | **Validated** | Masaryk University CJV, "Expressing Time"; CZECH ONLINE (Erasmus+) "Telling the time"; IJP id 785 |
| SK | **Validated** | KSSJ 2003: *štvrť na šesť*, *je pol deviatej*, *tri štvrte (trištvrte) na päť* |
| UK | **Validated** | Б. Антоненко-Давидович, *Як ми говоримо* (перша година; чверть на третю / чверть по другій; пів на сьому; за чверть сьома / чверть до сьомої) |
| PL | **Validated** | PWN Wielki słownik ortograficzny *wpół do (ósmej)*; PWN *za pięć dwunasta* (idiom, KJP corpus); ZPE (feminine ordinal hours); Babbel (*dwadzieścia po dziesiątej*) |
| RU | **Validated** | Ожегов, Ушаков, Ефремова: *четверть второго*, *три часа без четверти*, *половина шестого* |
| ES | **Validated** | RAE/ASALE DPD "hora²" §2.1.1 |
| IT | **Validated** | Treccani vocabolario *ora²* (*le dieci e cinque … e un quarto … e venti … e mezzo … le undici meno cinque*); Accademia della Crusca |
| PT | **Corrected** | Ciberdúvidas, "Minutos para a hora" (2025; *são três menos dez*, *são três menos um quarto*); Priberam *quarto* (*são três e um quarto*); Ciberdúvidas "Eram duas horas da tarde". European Portuguese only |
| GL | **Validated** | Xunta (SXPL), *Aula de Galego 1* (*A unha e dez*, *As nove menos vinte*, *As sete e cuarto*, *As dez menos cuarto*, *As oito e media*); RAG dictionary *hora* (*Son as catro*) |
| RO | **Validated** | DLRLC via dexonline: *sfert* ("Trenul sosește la unu și un sfert"), *fără* ("Este ora cinci fără zece minute") |
| EL | **Validated** | ΛΚΝ: *μισός* ("Η ώρα είναι μία / δύο / τρεις και μισή"), *παρά* ("στις τέσσερις παρά δέκα") |
| FI | **Validated** | Kielitoimiston sanakirja: *puoli* ("kello puoli kahdeksan … klo 7.30"), *vailla/vaille*, *vartti*; OPH, *Lukumatka jatkuu* 13 |
| TR | **Validated** | TDK "Sayıların yazılışı" rule 1 (*saat dokuzu beş geçe*, *saat yediye çeyrek kala*); TDK sözlük *çeyrek* (*dokuza çeyrek vardı*), *geçmek* (*saat onu geçiyordu*), *buçuk* |
| HE | **Validated** | Academy dictionary *שָׁעָה* (*השעה עשר ורבע*); University of Vermont Hebrew 1, lesson 19 (*Reva Leshtayim*, *Shtayim VaChetzi*) |
| FA | **Validated** | UT Austin Persian Online Resources (*ساعت یک و ربع*, *دو و نیم*, *ربع کم*); Chai & Conversation (*یک ربع به ده*) |
| HI | **Validated** | UNC *A Door Into Hindi* 14.4 (सवा, पौने incl. *पौने एक* for 12:45, साढ़े not with एक/दो, डेढ़, ढाई); Kamta Prasad Guru §175; HindiPod101 |
| JA | **Validated** | デジタル大辞泉 *半* (午前六時半); X時Y分 |
| ZH | **Validated** | 汉典: 两 before measure words, 点 "计时的单位", 刻 = 十五分钟; elon.io |
| VN | **Validated** | vtudien Việt-Việt: *giờ* (Bây giờ là mười giờ), *rưỡi*, *kém* (Ba giờ kém mười) |
| ZU | **Validated (two independent sources)** | Unisa, *Learn online Zulu* Theme 4; Department of Basic Education, *IsiZulu Grade 4 and 5 Mathematics Terminology* (*imizuzu emi-5 ngaphambi kwehora lesi-8*, *… lidlulile ihora lesi-8*, *ukugamanxa kwehora*) |

### Deliberate deferrals kept

SW and ZU ordinals (obligatory noun-class concord), EE ClockTime (no sourced minute convention), WO
ClockTime (competing conventions), EE ordinal of zero, WO ordinals of zero and of the round thousands:
unchanged, each pinned by its "not supported" scenario. No new evidence was found that would change them.

### Out of scope, opened as tickets

NTS-27 (cardinal defects found on the way), NTS-28 (compound ordinals not built by the declarative
pipeline), NTS-29 (attested zero ordinals not enabled; closed 2026-10-11, see "NTS-29 and NTS-32" above). See
`TODO.md`.

## NTS-21 — Wolof large scales (2026-10-09)

- **Anchors.** `milyoŋ` (10^6) and `milyaar` (10^9), French loans. **Consulted, full text**: Wolof Bible
  (eBible `wolmbs` NT PDF, Mt 25:15–28: `juróomi milyoŋ`, `ñaari milyoŋ`, `benn milyoŋ`, `fukki milyoŋ`); Wolof
  Ajami Reader (F. Ngom, D. Rodima-Taylor, J. Yanco, E. D. Diagne, Boston University, 2025: `juróomi milyaar ak
  juróom ñent téeméeri milyoŋ …`, `juróom fukki milyoŋ` = 50.000.000 Faraŋ); Boston University, The 200 Word
  Project (`Fukki Téémééri Junni / Benn Milyoŋ`); Kosogorova 2023 (stops at `junni`). **Checked in the PR #628
  review** (not reachable from this session): M. Guérin, *Système de numération en wolof* (French borrowings most
  frequent above 1000: `milyoŋ`, `milyaar`); Wolof university course (`milyoŋ (tamndareet)`, `milyaar
  (tamñareet)`). Rejected spellings: `milyon` (the former unused configuration suffix), `million`, `miliyar`.
- **Scale.** `milyaar` at 10^9 makes the borrowed system the long scale: 10^(6n) `-yoŋ`, 10^(6n+3) `-yaar`.
- **Project rule** (as NTS-24/NTS-25). Wolof has adopted the `milyoŋ` / `milyaar` family. The library extends this
  family productively with Conway-Guy-Wechsler on the long scale using Wolof-compatible spellings. Generated
  higher names are a library generation convention, not a claim that every generated form is independently
  attested in Wolof usage.
- **Transliteration.** qu → kw, x → ks, v → w, soft c → s, hard c → k, -on → -yoŋ, -ard → -yaar; groups joined
  by `li`, empty group `ni`, suffix joined by `l` (new `suffixSeparator` attribute). Tables and reference names:
  `DONE-2026-10-09.md`.
- **Multipliers.** One is the bare `benn milyoŋ`; a multiplier above one takes the NTS-19 connective `-i` on its
  last element, as for `téeméer` and `junni`. Attested: `ñaari`, `juróomi`, `fukki milyoŋ` (Bible),
  `juróom fukki milyoŋ`, `juróomi milyaar` (Ajami Reader); the rest of 1–999 is productive. Variant not adopted:
  Ajami Reader `juróom ñent téeméeri milyoŋ` (no `-i` before `téeméer`; NTS-19 keeps `juróom ñenti téeméer`).
- **Ordinals.** Unchanged (1–999 999): no ordinal above the thousands is exemplified.

## NTS-25B — Conway tables localized to ID/MS, SW and TR spelling (2026-10-08)

- **Project rule.** A language that has integrated the million/billion/trillion family under identifiable local
  forms gets a productive Conway-Wechsler extension in its own spelling; the prefixes are adapted with the same
  mechanisms as its attested loans. Two kinds of names are therefore produced: **attested local forms** (listed
  below with their source) and **project-authorized Conway extrapolations** (every other name, including all
  n = 1000 names such as `miliniliun`, `milinilion`, `milinilioni`, `milnilyon`), which are not presented as
  normative.
- **ID.** Attested (KBBI, via detik.com and penerbitdeepublish.com summaries of the KBBI entries): `triliun`,
  `kuadriliun`, `kuintiliun`, `sekstiliun`, `septiliun`, `oktiliun`, `noniliun`, `desiliun`. Rules: qu → ku,
  x → ks, soft c → s, hard c → k; junction `li` + suffix `un`.
- **MS.** Attested (DBP PRPM, already cited above for `bilion`, `trilion`): `trilion`, `kuadrilion`,
  `kuintilion`, `sekstilion`, `septilion`, `oktilion`. Same tables as ID (`baseOn="ID"`), suffix `on`.
- **SW.** Attested (Wiktionary Swahili entries, "borrowed from English"): `kwadrilioni`, `kwintilioni`,
  `sekstilioni`, `desilioni`; `milioni`, `bilioni`, `trilioni` were already in use. Rules: qu → kw, x → ks, soft
  c → s, hard c → k (Swahili writes no q or x, and c only in ch). Contradicting source not followed: the
  `tarakimu` Python library (`kuadrilioni`, `kuintilioni`, `seksitilioni`).
- **TR.** Attested (tr.wikipedia, *Büyük sayıların adları*; webtekno.com and sorubak.com for the first terms):
  `trilyon`, `katrilyon`, `kentilyon`, `sekstilyon`, `septilyon`, `oktilyon`, `nonilyon`, `desilyon`,
  `undesilyon`, `dodesilyon`, `tredesilyon`, `katordesilyon`, `kendesilyon`, `seksdesilyon`, `septendesilyon`,
  `oktodesilyon`, `novemdesilyon`, `vigintilyon`, `trigintilyon`, `katragintilyon`, `kenkagintilyon`,
  `sentilyon`. Morphemes taken from them: qu → k (`katri`, `kenti`, `kator`, `katragint`, `kenkagint`, units
  `kenka`), duo → `do`, x → ks, c → s (Turkish c reads /dʒ/). The list is the dictionary series; the strict
  Conway structure (NTS-24) keeps `kenkadesilyon`, `sedesilyon`, `novendesilyon` for n = 15, 16, 19. NTS-25A's
  `seksi` was wrong (`sekstilyon` is the attested form) and is corrected to `seksti`.
- **TR n = 1000.** `milnilyon` is kept; `VoidGroup="ini"` (`milinilyon`) was evaluated and not adopted, because
  it changes only zero groups (`milmilyon`, `mildesilyon` keep the bare `l`) and Turkish accepts `ln` inside a
  word (`yalnız`). See `DONE-2026-10-08(2).md`.

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
- **Deferred to NTS-25B (owner decision; done, see the NTS-25B section).** ID/MS units/tens/hundreds tables, and the TR/SW Latin
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
- Речник на българския език (ИБЕ-БАН), `и`: <https://ibl.bas.bg/rbe/lang/en/%D0%B8>
- БЕРОН, “Съставни числителни имена”: <https://beron.mon.bg/rechnik/1627>
- ИБЕ-БАН, “За една особеност в употребата на числителните имена”: <https://ibl.bas.bg/ezikovi_spravki/za-edna-osobenost-v-upotrebata-na-tchislitelnite-imena/>
- bulgaro.io, “Counting to 1000”: <https://www.bulgaro.io/learn-bulgarian/counting-to-1000>
- bg.wikipedia, “Имена на числата”: <https://bg.wikipedia.org/wiki/%D0%98%D0%BC%D0%B5%D0%BD%D0%B0_%D0%BD%D0%B0_%D1%87%D0%B8%D1%81%D0%BB%D0%B0%D1%82%D0%B0>
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
- NTS-08 final validation (2026-10-09):
  - Språkrådet, Frågelådan: <https://frageladan.isof.se/faqs/27138>, <https://frageladan.isof.se/faqs/31080>, <https://frageladan.isof.se/faqs/21216>, <https://frageladan.isof.se/faqs/22011>; SAOB: <https://www.saob.se/>
  - Dansk Sprognævn: <https://sproget.dk/sprogviden/spoergsmaal-og-svar/dansk-sprognaevn/sv00000084/>, SV00000047, SV00017853, SV00001178; Den Danske Ordbog: <https://ordnet.dk/ddo/ordbog?query=millionte>; Systime, Talnavne: <https://sprogligmatematik.ibog.gyldendal.dk/fileadmin/indhold/Filer_til_download/Talnavne.pdf>; Langenscheidt DA-DE: <https://en.langenscheidt.com/danish-german/klokke>
  - Bokmålsordboka API: <https://ord.uib.no/>
  - A magyar helyesírás szabályai (AkH12): <https://helyesiras.mta.hu/helyesiras/default/akh12>; MTA "Számok": <https://helyesiras.mta.hu/helyesiras/default/numerals>; Giay & Nádor 1998: <https://vmek.oszk.hu/01700/01702/01702.pdf>
  - Український правопис 2019, §38: <https://2019.pravopys.net/sections/38/>; goroh.pp.ua: <https://goroh.pp.ua/>; Антоненко-Давидович: <https://webpen.com.ua/pages/Antonenko-Davudovich/12_Antonenko-Davidovich_numerals.html>
  - MSU *Basic Persian* §1.8: <https://openbooks.lib.msu.edu/persian/chapter/1-8-numbers-1-100-ordinal-and-cardinal-numbers-in-persian-language>; Dehkhoda via vajehyab: <https://vajehyab.com/dehkhoda/>; UT Austin: <https://sites.la.utexas.edu/persian_online_resources/numbers-1/to-tell-the-time/>
  - TDK: <https://sozluk.gov.tr/>; <https://tdk.gov.tr/icerik/yazim-kurallari/sayilarin-yazilisi/>
  - M. Bussey, Arabic numbers: <https://allthearabicyouneverlearnedthefirsttimearound.com/p3/arabic-numbers/>
  - Academy of the Hebrew Language: <https://hebrew-academy.org.il/פרק-אחד-עשר-או-פרק-אחת-עשרה/>; UVM Hebrew 1, lesson 19: <https://www.uvm.edu/~gbavly/lso19/lsn19.html>
  - IJP (ÚJČ AV ČR): <https://prirucka.ujc.cas.cz/?id=785>, <https://prirucka.ujc.cas.cz/?id=791>; Masaryk University: <https://www.cjv.muni.cz/media/3402805/expressing-time.docx>; CZECH ONLINE: <https://www.czechonline.org/upload/Downloads/files/table/19/item/29/161/g-11-hodiny-en.pdf>
  - JÚĽŠ SAV dictionaries: <https://slovnik.juls.savba.sk/>
  - Taaladvies, "Aaneenschrijven van telwoorden (algemeen)": <https://taaladvies.net/aaneenschrijven-van-telwoorden-algemeen/>; Woordenlijst: <https://woordenlijst.org/>
  - ZPE, "Odmiana liczebnika i zaimka": <https://zpe.gov.pl/a/odmiana-liczebnika-i-zaimka/DoluLQfXb>; PWN: <https://sjp.pwn.pl/>
  - Правила русской орфографии и пунктуации: <https://gramota.ru/biblioteka/spravochniki/pravila-russkoy-orfografii-i-punktuatsii/imena-chislitelnye-2201>; Ожегов via gufo.me: <https://gufo.me/dict/ozhegov/>
  - RAE/ASALE DPD: <https://www.rae.es/dpd/ordinales>, <https://www.rae.es/dpd/hora>; Priberam: <https://dicionario.priberam.org/>; Ciberdúvidas: <https://ciberduvidas.iscte-iul.pt/consultorio/perguntas/minutos-para-a-hora-a-um-quarto-para/38623>
  - RAG/ILG NOMIG (2003): <https://publicacions.academia.gal/index.php/rag/catalog/download/216/212/101?inline=1>; RAG dictionary: <https://academia.gal/dicionario>; Xunta, *Aula de Galego 1*: <https://www.lingua.gal/documents/204205/218459/Manual_Aula_de_Galego_1_libro_completo_red.pdf>
  - Λεξικό της Κοινής Νεοελληνικής: <https://www.greek-language.gr/greekLang/modern_greek/tools/lexica/triantafyllides/>; Treccani *ora²*: <https://www.treccani.it/vocabolario/ora2/>; dexonline: <https://dexonline.ro/>
  - Aalto University, "Numeroiden taivutus": <https://mycourses.aalto.fi/pluginfile.php/29619/mod_page/content/9/Numeroiden%20taivutus.pdf>; Kielitoimiston sanakirja: <https://www.kielitoimistonsanakirja.fi/>; OPH: <https://www.oph.fi/fi/oppimateriaali/lukumatka-jatkuu-opettajan-opas/sisallys/13-mita-kello>
  - Kamta Prasad Guru, हिंदी व्याकरण: <https://hi.wikisource.org/wiki/हिंदी_व्याकरण/विशेषण>; UNC *A Door Into Hindi*: <https://taj.oasis.unc.edu/Hindi.Less.14/grammar04.html>
  - kotobank (デジタル大辞泉): <https://kotobank.jp/word/%E7%AC%AC-560447>; 한글 맞춤법 summary: <https://www.scourt.go.kr/portal/gongbo/PeoplePopupView.work?gubun=24&seqNum=1545>; 汉典: <https://www.zdic.net/hans/%E7%AC%AC>; vtudien: <https://vtudien.com/viet-viet/dictionary/>
  - Euskaltzaindia: <https://www.euskaltzaindia.eus/dok/arauak/Araua_0018.pdf>, <https://www.euskaltzaindia.eus/dok/arauak/Araua_0007.pdf>
  - Речник на българския език (ИБЕ БАН): <https://ibl.bas.bg/rbe/>; Školski rječnik hrvatskoga jezika: <https://rjecnik.hr/>; lets-learn.eu: <https://www.lets-learn.eu/croatian/guide/time-in-croatian>
  - Department of Basic Education, isiZulu Grade 4–5 Mathematics Terminology: <https://www.education.gov.za/Portals/0/Documents/MTbBE/mttbe%20terminology/IsiZulu%20%20Grade%204%20and%205%20Mathematics%20Terminology.pdf>
- OQLF, Banque de dépannage linguistique, "Écriture des fractions": <https://vitrinelinguistique.oqlf.gouv.qc.ca/25138/la-typographie/nombres/ecriture-des-fractions>
- OQLF, Banque de dépannage linguistique, "Nom singulier après un nombre fractionnaire inférieur à deux": <https://vitrinelinguistique.oqlf.gouv.qc.ca/21582/la-grammaire/le-nom/nombre-des-noms-dans-certains-emplois/cas-particuliers-du-nombre-du-nom/nom-singulier-apres-un-nombre-fractionnaire-inferieur-a-deux>
- Académie française, "« Vingt-cinq millièmes » ou « vingt cinq-millièmes » ?" (2022): <https://www.academie-francaise.fr/vingt-cinq-milliemes-ou-vingt-cinq-milliemes>
- Dictionnaire de l'Académie française, 9e éd., "millionième": <https://www.dictionnaire-academie.fr/article/A9M2188>; "milliardième": <https://www.dictionnaire-academie.fr/article/A9M2177>
- Éduscol, *Fractions et nombres décimaux au cycle 3*: <https://eduscol.education.fr/document/16510/download>
- OpenStax, *Prealgebra 2e*, §5.1 "Decimals": <https://openstax.org/books/prealgebra-2e/pages/5-1-decimals>
- PH Niederösterreich, G. Egger, *Dezimalzahlen* (2022): <https://noegi.ph-noe.ac.at/fileadmin/noegi_files/GeoGebra-Books_und_-Arbeitsbl%C3%A4tter/kl1-dezimalzahlen.pdf>
- Internetová jazyková příručka, "Počítaný předmět po číslovkách": <https://prirucka.ujc.cas.cz/?id=792>; "Členění čísel": <https://prirucka.ujc.cas.cz/?id=791>; entries <https://prirucka.ujc.cas.cz/?slovo=tis%C3%ADc>, <https://prirucka.ujc.cas.cz/?slovo=milion>, <https://prirucka.ujc.cas.cz/?slovo=miliarda>
- I. Turinská, "Shoda u českých složených základních číslovek obsahujících číslovku jeden": <https://ucjtk.ff.cuni.cz/wp-content/uploads/sites/57/2015/11/ZS2009_rukovet_09_Turinska.pdf>, <https://ucjtk.ff.cuni.cz/wp-content/uploads/sites/57/2015/11/ZS2009_text_09_Turinska1.pdf>
- Pravidlá slovenského pravopisu (2000): <https://www.juls.savba.sk/ediela/psp2000/psp.pdf>; JÚĽŠ dictionary portal (KSSJ, PSP 2013, OGS 2022): <https://slovnik.juls.savba.sk/?w=dvetis%C3%ADc>
- A. Jarošová, "Zložené číslovky so zreteľom na písanie spolu a oddelene", Slovenská reč 86/2 (2021): <https://www.sav.sk/journals/uploads/12101101zlozene-cislovky-so-zretelom-na-pisanie-spolu-a-oddelene.pdf>
- ИБЕ-БАН, "За една особеност в употребата на числителните имена": <https://ibl.bas.bg/ezikovi_spravki/za-edna-osobenost-v-upotrebata-na-tchislitelnite-imena/>; bg.wikipedia, "Бройна форма на съществителните в мъжки род в българския език": <https://bg.wikipedia.org/wiki/%D0%91%D1%80%D0%BE%D0%B9%D0%BD%D0%B0_%D1%84%D0%BE%D1%80%D0%BC%D0%B0_%D0%BD%D0%B0_%D1%81%D1%8A%D1%89%D0%B5%D1%81%D1%82%D0%B2%D0%B8%D1%82%D0%B5%D0%BB%D0%BD%D0%B8%D1%82%D0%B5_%D0%B2_%D0%BC%D1%8A%D0%B6%D0%BA%D0%B8_%D1%80%D0%BE%D0%B4_%D0%B2_%D0%B1%D1%8A%D0%BB%D0%B3%D0%B0%D1%80%D1%81%D0%BA%D0%B8%D1%8F_%D0%B5%D0%B7%D0%B8%D0%BA>
- Український правопис 2019, § 70: <https://pravopys.net/sections/70/>; Profpressa, "Узгодження числівника й іменника": <https://profpressa.com/articles/uzgodzhennia-chislivnika-i-imennika-sekreti-pravopisu/print>
- Unicode CLDR plural rules (cs sk, ru uk, bg): <https://github.com/unicode-org/cldr/blob/main/common/supplemental/plurals.xml>
- NTS-30C (2026-10-14):
  - IJP (ÚJČ AV ČR), entries: <https://prirucka.ujc.cas.cz/?slovo=bilion>, <https://prirucka.ujc.cas.cz/?slovo=trilion>, <https://prirucka.ujc.cas.cz/?slovo=kvadrilion>, <https://prirucka.ujc.cas.cz/?slovo=biliarda>
  - KSSJ 4 via slovnik.aktuality.sk, *bilión*: <https://slovnik.aktuality.sk/pravopis/kratky-slovnik/?q=bili%C3%B3n>; Slovak Wikipedia, "Biliarda": <https://sk.wikipedia.org/wiki/Biliarda>
  - ru.wiktionary, *триллион*: <https://ru.wiktionary.org/wiki/%D1%82%D1%80%D0%B8%D0%BB%D0%BB%D0%B8%D0%BE%D0%BD>; *квадриллион*: <https://ru.wiktionary.org/wiki/%D0%BA%D0%B2%D0%B0%D0%B4%D1%80%D0%B8%D0%BB%D0%BB%D0%B8%D0%BE%D0%BD>
  - Горох, *трильйон* (словозміна): <https://goroh.pp.ua/%D0%A1%D0%BB%D0%BE%D0%B2%D0%BE%D0%B7%D0%BC%D1%96%D0%BD%D0%B0/%D1%82%D1%80%D0%B8%D0%BB%D1%8C%D0%B9%D0%BE%D0%BD>; uk.wiktionary, *трильйон*: <https://uk.wiktionary.org/wiki/%D1%82%D1%80%D0%B8%D0%BB%D1%8C%D0%B9%D0%BE%D0%BD>
  - Речник на българския език, *трилион*: <https://rechnik.chitanka.info/w/%D1%82%D1%80%D0%B8%D0%BB%D0%B8%D0%BE%D0%BD>; *квадрилион*: <https://rechnik.chitanka.info/w/%D0%BA%D0%B2%D0%B0%D0%B4%D1%80%D0%B8%D0%BB%D0%B8%D0%BE%D0%BD>
