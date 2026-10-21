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
| BG | BG, BG-BG | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `BulgarianOrdinalLanguageSpecifics` (int range, gendered) | Yes | 15 / 12 | `gender` (standalone/masculine/feminine/neuter) | **Consulted**: ezik.bg, “Числително име” (`хиляда сто и втори`, gender endings); dumite-bg.com, entry `двехиляден` | Round thousands with a multi-word multiplier and round millions above one are declined (unverified compound adjectives); NTS-30B: *хиляди*, *милиона*, *милиарда* after a multiplier, *един милион*; NTS-30D: *и* before the last component of every numeral phrase (*двадесет и едно*, *хиляда и сто*, *двадесет и една хиляди*); NTS-30H: fractions with the ordinal denominator (*една втора*, *две трети*, *една четвърт*, *седем осми*); NTS-39: plural after a compound ending in *една* (*сто и една хилядни*) |
| HR | HR, HR-HR | SCALE-LONG | Yes (repaired) | Declarative last-word rules + `CroatianOrdinalLanguageSpecifics` (rejects unverified round scales) | Yes | 15 / 12 | none | **Consulted**: Hrvatski pravopis (IHJJ), “Višerječnice”, rule 31 (`tisuću jedan`, `tisuću tristo jedanaest`, `tri tisuće sedamsto trideset tri`, `milijun petsto trideset tisuća`, `dvije milijarde trideset tri milijuna …`; ordinals `tisuću prvi`, `tri tisuće sedamsto trideset treći`); Hrvatski jezični portal, entries `tisućiti`, `milijunti`, `milijarditi`. Clock **validated (weakest evidence, documented decision)**: Školski rječnik hrvatskoga jezika (*sat*, *sata*, *sati*); lets-learn.eu (`pola deset`, `petnaest do devet`) | Masculine nominative ordinals only; every round scale value other than the lexical units 1 000 / 1 000 000 / 1 000 000 000 fails closed as an ordinal. A single thousand followed by a remainder is `tisuću` wherever it stands (`milijun tisuću jedan`) |
| HU | HU, HU-HU | SCALE-LONG | Yes (repaired) | `HungarianOrdinalLanguageSpecifics` (int range) | Yes | 15 / 12 | none | **Corrected** (NTS-08 final): AkH12 §291; MTA "Számok" tool (AkH12-292); Giay & Nádor 1998 (clock) | The former `suffix="."` pseudo-ordinals are gone; NTS-08: A single million or milliard keeps *egy* (*egymilliomodik*) |
| CS | CS, CS-CZ | SCALE-LONG | Yes | `CzechOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (standalone/mužský/ženský/střední) × `case` | **Validated** (NTS-08 final): IJP (ÚJČ AV ČR) id 785, 791 and slovník; cs.wiktionary paradigms; Masaryk University and CZECH ONLINE (clock) | Ordinals verified up to 9 999; round millions/milliards only for one; NTS-30B: *tisíc/milion/miliarda* agree (*dva tisíce, jeden milion, dvě miliardy, pět milionů*; IJP id 792); NTS-30H: fractions with the IJP fraction nouns (*jedna třetina, dvě třetiny, pět sedmin*); NTS-39: every IJP noun, genitive plural after compounds (*dvacet dva třetin*), *lomeno* + instrumental for 2–999 (*jedna lomeno dvaceti třemi*); NTS-36: Conway names in Czech spelling (*kvadrilion*, *oktilion*, *kvatuordecilion*); NTS-42 F: fractions from 10^12 decline the converter's generated nouns (*jednomu bilionu třetin*, *jedna lomeno pěti biliardami*) |
| SK | SK, SK-SK | SCALE-LONG | Yes | `SlovakOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` (mužský/ženský/stredný) × `case` | **Validated** (NTS-08 final): JÚĽŠ SAV (KSSJ, PSP, OGS) for the declension and the clock (*štvrť na*, *pol*, *tri štvrte na*); teraz.sk | Ordinals verified up to 9 999 and one million; NTS-30B: cardinals in the PSP one-word spelling (*dvadsaťdva, päťsto, dvetisíc*), *milióny/miliardy* agree; NTS-30H: fractions with the fraction nouns (*jedna tretina, dve tretiny, päť tretín*); NTS-39: full noun paradigms, *dvadsaťdva tretín*, *nula tretín*, *lomené* + instrumental of the last component (*sto štyridsiatimi dvoma*) or *lomené číslom* when it is indeclinable, up to 999 999; NTS-36: Conway names in Slovak spelling (*kvadrilión*, *kvintiliarda*, *oktilión*); NTS-42 F: fractions from 10^12 decline the converter's generated nouns (*jednému biliónu tretín*, *jedna lomené dvoma biliónmi*), the genitive singular of *-ón* fail-closed |
| PL | PL | none | Yes (existing) | `PolishOrdinalLanguageSpecifics` + declarative | Yes | 5 / 12 | rodzaj × przypadek | **Corrected** (NTS-08 final): ZPE (MEN) "Odmiana liczebnika i zaimka"; PWN (*wpół do*, *za pięć dwunasta*); Babbel | —; NTS-08: Ordinals 1–1999 (1000–1999 rebuilt); from 2000 fail closed |
| RU | RU | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes (existing) | Declarative + `RussianOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | gender × case | **Validated** (NTS-08 final): Правила (2006) §132–133; Ожегов, Ушаков, Ефремова (clock) | Ordinal of zero `нулевой` (NTS-14); NTS-08: Ordinals 0–1999; from 2000 fail closed |
| UK | UK, UK-UA | SCALE-SHORT-CYRILLIC (NTS-25A; was SCALE-LONG) | Yes | `UkrainianOrdinalLanguageSpecifics` (gender × case) | Yes | 15 / 12 | `gender` × `case` (Ukrainian values) | **Corrected** (NTS-08 final): Український правопис 2019 §38; goroh.pp.ua paradigms; Антоненко-Давидович (clock) | Round thousands verified up to 10 000; ASCII apostrophe as in the cardinals; NTS-08: Single million/milliard without *один*; round thousands up to 10 000 only; NTS-30B: *тисяча/мільйон/мільярд* agree (*дві тисячі, два мільйони, п'ять мільйонів*); NTS-30H: fractions after Правопис 2019 §107 (*одна третя, дві третіх, дев'ять десятих*); NTS-39: singular after a compound ending in *одна* (*тридцять одна сота*) |
| ES | ES | none | Yes (existing) | Declarative + `SpanishOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender × form | **Corrected** (NTS-08 final): RAE/ASALE DPD "ordinales" and "hora" | Exact `Convert(TimeOnly)` unchanged; NTS-08: Ordinals 1–29, round tens, 100, 1000; cardinals 22/23/26 accented; other values fail closed; NTS-30G: the multiplier of *millón/millones* is masculine under any caller gender (DLE *millón*, DPD "cardinales" §3, §6) |
| IT | IT | SCALE-LONG | Yes: 1–1999 except 1110–1910, round thousands 2000–999000, 100001–100009, round multiples of milione/miliardo/bilione/biliardo/trilione | Declarative `<OrdinalStem>`, `<OrdinalComposition>`, `<OrdinalScale>` + `ItalianOrdinalLanguageSpecifics` (domain guard) | Yes | 5 / 12 | gender | Clock **validated** (NTS-08 final): Treccani vocabolario *ora²* (full five-minute table); Accademia della Crusca (`l'una e mezzo`, `le due meno un quarto`, `le otto meno venti`) | Cardinals soldered with `<Fusion>` (NTS-10); compound ordinals by `<OrdinalStem>` (NTS-13, see below), analytic 1010 and 100001–100009 (NTS-15) and round scale ordinals through trilione (NTS-17, NTS-18) (see below); zero, 1110–1910, the other non-round thousands above 1999 and the non-round values from a million deliberately fail closed as ordinals (Italian closure, see below); millions and above are separate nouns joined by `e` (NTS-14, see below) |
| PT | PT | none | Yes (existing) | Declarative + `PortugueseOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender | **Corrected** (NTS-08 final): Priberam; Ciberdúvidas "Minutos para a hora", "Eram duas horas da tarde" | Deliberate direct numeric reading; `para`/`menos` constructions not produced; PT-PT/PT-BR not split; NTS-08: European Portuguese clock (*menos* after the half hour); ordinals 1–20, round tens, 100, 1000 |
| GL | GL, gl-ES | none | Yes (existing) | Declarative + `GalicianOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 5 / 12 | gender | **Corrected** (NTS-08 final): RAG/ILG NOMIG 2003 §16.2; RAG dictionary; Xunta *Aula de Galego 1* (clock) | —; NTS-08: Ordinals 1–20, round tens, 100, 1000 |
| RO | RO, RO-RO | none | Yes | `RomanianOrdinalLanguageSpecifics` (DOOM) | Yes | 15 / 12 | `gen` | **Consulted**: dexonline/DOOM entries `sutălea` (`al (o) sutălea`, `a (o) suta`, `al două sutelea`) and `miilea` (`al o miilea`, `a o mia`, `al două miilea`); reference usage for the clock (`ora două`, `două fără un sfert`) | Ordinals up to 999 999 and one million (masculine); round `de mii` thousands declined |
| EL | EL | none | Yes (existing) | Declarative + `GreekOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | gender (feminine cardinals added) | **Corrected** (NTS-08 final): Λεξικό της Κοινής Νεοελληνικής (ordinals, μισός, παρά) | Masculine cardinals (`ένας`) not modelled; feminine cardinal words mirrored in ordinal rules so ordinals are unchanged; NTS-08: Ordinals 1–20, round tens and hundreds, 1000; 13–19 agree in both parts |
| FI | FI | none | Yes (existing) | Declarative + `FinnishOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | case | **Validated** (NTS-08 final): Aalto University "Numeroiden taivutus"; Kielitoimiston sanakirja; OPH | —; NTS-08: Ordinals: nominative only, 0–20, round tens and hundreds, 1000; NTS-30E: one word inside a group and multiplier + *tuhat/tuhatta* (*kaksikymmentäyksi*, *kaksituhatta*), one space between groups, every component inflected in the partitive and genitive (Kielitoimiston ohjepankki) |
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
| EU | EU, eu-ES | none | Yes (existing) | Declarative + `BasqueOrdinalLanguageSpecifics` (NTS-08 domain guard) | Yes | 15 / 12 | none | **Corrected** (NTS-08 final): Euskaltzaindia rules 7 and 18; Sareko Euskal Gramatika (clock) | Clock-case forms are literal per hour; cardinals unchanged; NTS-08: Ordinals 1–1000 (*bosgarren*); zero and > 1000 fail closed; NTS-30G: one *eta* before the last group of each numeral phrase (rule 203 §4: *hirurogei milioi berrehun mila eta bat*) |
| SW | SW, SW-KE, SW-TZ | SCALE-SHORT | **Deferred** | — | Yes | 15 / 12, `hourOffset=-6` | none | **Consulted**: Five Colleges LangMedia, “Swahili – Tanzania – Telling Time”; SpokenSwahili, “Telling the time in Swahili” | No day-part words (asubuhi, mchana, jioni, usiku) |
| ZU | ZU | none | **Deferred** | — | Yes | 15 / 12 | none | **Validated** (NTS-08 final, two sources): Unisa *Learn online Zulu* Theme 4; DBE *IsiZulu Grade 4 and 5 Mathematics Terminology* | Hour forms are literal ClockTime patterns; they do not enable ordinals |
| EE | EE | SCALE-SHORT (scale tables only, NTS-23) | Yes (NTS-20): cardinal + `-lia` on the last element, `gbãtɔ` for 1 | Declarative + `ZeroOrdinalUnsupportedLanguageSpecifics` | **Deferred** | — | none | **Consulted** (NTS-20): Dzablu-Kumah, *Basic Ewe for Foreign Students*; Biblica Open Ewe Contemporary Scriptures; *Ewe Basic Course* 1968; Peace Corps Togo 2010; Wiktionary | Cardinals unbounded (NTS-23): static `akpe`, then the Conway-Wechsler short scale `miliɔn`, `biliɔn`, `triliɔn` (sourced anchors), `quadriliɔn` … (project extrapolation), each before its standalone multiplier (`multiplierPosition="afterScale"`); zero ordinal unsupported |
| WO | WO | none | Yes (`-éel`, NTS-16, NTS-19) | Declarative + `WolofOrdinalLanguageSpecifics` | **Deferred** | — | none | No consistent source found | Cardinals unbounded (NTS-21): static `junni`, then the long-scale Conway-Wechsler names `milyoŋ`, `milyaar` (attested anchors), `bilyoŋ`, `bilyaar` … (project extrapolation in Wolof spelling); ordinals 1–999 999 only, no ordinal of zero nor of the round thousands |

## NTS-47 — last audit of NTS-42: SK generated *-ón* genitive, D and E/CS (2026-10-21)

Scope: the three closures NTS-42 kept after NTS-46. Old/new outputs and the dump: `DONE-2026-10-21(2).md`. Evidence
levels as in the NTS-36 section below.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | SK | *Pravidlá slovenského pravopisu*, 1940 (JÚĽŠ e-dition, <https://www.juls.savba.sk/ediela/psp1940/psp1940.pdf>), pp. 254, 144, 412, 237 | **attested** *milión, -a, m.*; *bilión, -a, m.*; *trilión, -a, m.*; but *kvadrilión, -u, m., bilión biliónov* |
| 2 | SK | *Pravidlá slovenského pravopisu*, current text of the JÚĽŠ (slovnik.aktuality.sk, "Pravidlá slov. pravopisu", © JÚĽŠ SAV) | **attested** *bilión ‑a čísl. m.*, *kvadrilión ‑a m.*, *kvintilión ‑a m.*; no entry *trilión* (the search falls back to *triler*) |
| 3 | SK | KSSJ 4 (2003), *bilión -a m. čísl. zákl.* (NTS-42 F #5) | **attested** *bilióna* |
| 4 | SK | L. Navrátil, "Neohybnosť v ohybných slovných druhoch", *Kultúra slova* 37 (2003), 6, 336–342 (<https://www.juls.savba.sk/ediela/ks/2003/6/ks2003-6.html>), p. 341 | **rule**: "Číslovky milión, bilión, trilión majú substantívnu povahu a skloňujú sa podľa vzoru dub" (no case form quoted) |
| 5 | SK | Slovník cudzích slov online (slovnik.aktuality.sk), *bilión*, *trilión*, *kvadrilión*, *kvintilión*, *sextilión*, *septilión* | definitions only, **no grammatical marks**: the *trilión -u*, *kvadrilión -u* once attributed to it in a test comment are not in it (corrected) |
| 6 | CS | J. Sláma, "Operátor lomeno, koordinátor lomeno, adordinátor: K funkcím slova lomeno", *Naše řeč* 105 (2022), 1, 49–55 (<http://nase-rec.ujc.cas.cz/archiv.php?art=8661>; full text on CEEOL only) | abstract read only: *lomeno* names the sign, is a mathematical operator, a coordinator ('or') and an adordinator; **full text not accessible** here (CEEOL paywall, ResearchGate refused, no PDF in the Naše řeč archive) |
| 7 | CS | ÚJČ poradna database (dotazy.ujc.cas.cz), searches *lomeno* and *nula* (keyword, question, case, answer) | *lomeno*: no public answer; *nula*: "obecné podstatné jméno" (#7437, project name), a time read *ve dvacet tři nula pět* (vokalizace), *nula bodů* in a question — nothing on *nula* + counted noun in an oblique case |
| 8 | CS | CzechEncy, *Nový encyklopedický slovník češtiny*, "základní číslovka" (<https://www.czechency.org/slovnik/ZÁKLADNÍ ČÍSLOVKA>) | *nula* only as the value between positive and negative (*teplota nula stupňů*, nominative); *sto, milion, miliarda, bilion* decline after their noun patterns; nothing on an indeclinable quantifier *nula*; no entry *NULA* |

**SK *-ón* genitive — opened.** The series is one noun class (#4, *dub*) whose genitive singular, lexical in *dub*
(NTS-42 F #8), is *-a* for every member the dictionaries give: *milión*, *bilión*, *trilión* (#1), *bilión* (#2, #3),
*kvadrilión*, *kvintilión* (#2). The one divergent form, *kvadrilión, -u* (#1, 1940), is superseded by the current
*kvadrilión ‑a* (#2) of the same codification. The configuration declares `singular.genitive` = *óna* on the *-ón*
`<GeneratedScaleForm>`: *jedného bilióna tretín*, *jedného trilióna tretín*, *jedného kvadrilióna tretín*. Level:
**attested** for *bilión*, *trilión*, *kvadrilión*, *kvintilión*; **productive application by morphological class** above
(*sextilióna* … *milikvadrilióna*: the Conway prefix invariant, only the suffix fragment inflected). No C# change: the
plugin already read the key (`ReadScaleNoun`, `DeclineScale`).

**D — still not established.** #6 could not be read beyond its abstract, which names the functions of *lomeno* but says
nothing about its operands' case; #7 has no answer on *lomeno*. The sources of NTS-45 (Prandstetter 1938, SSJČ *dvě
lomeno třemi*, Ondrus 1968, Peciar 1976, SR 1978) read the phrase in isolation. The open question is **syntactic**, not
morphological: every candidate form of X exists (*dvěma*, *dvoma*), what is missing is whether the arithmetic term takes
the external case at all. **Fail-closed.**

**E/CS — still closed.** Lexical paradigm: *nula* is a feminine noun (*nuly, nule, nulu, nulou*; IJP, NTS-45 #4; #7
"obecné podstatné jméno"). Usage: an indeclinable quantifier occurs (*při nula stupních*, NTS-45; *s nula stupni
volnosti*, a VUT thesis cited by the request, not re-read here), which is **usage, not a norm**. No Czech normative or academic source (#7, #8, IJP "Počítaný předmět")
says which applies to a counted noun in an oblique case; the Slovak rule (Peciar 1958) is not evidence for Czech.
**Fail-closed.**

## NTS-46 — NTS-42 E/UK and F/UK: Ukrainian zero and generated-scale fractions (2026-10-21)

Scope: the two Ukrainian closures NTS-42 kept after NTS-45. Old/new outputs and the dump: `DONE-2026-10-21(1).md`.

| # | Source | What it establishes |
|---|---|---|
| 1 | Український правопис 2019, §107.1 (<https://slovnyk.ua/pravopys.php?prav_par=107>; official 2026 edition: <https://mon.gov.ua/static-objects/mon/sites/1/zagalna%20serednya/Pravopys.2019/ukrayinskii-pravopis-oficiine-vidannia-2026.pdf>) | **rule**: fractions "відмінюємо … як звичайні числівники" (*двом третім*, *трьома п'ятими*, *ділити на одну двадцяту*) |
| 2 | Правопис 2019, §105.6 and §105.11 (<https://slovnyk.ua/pravopys.php?prav_par=105>) | **rule**: every part of a compound cardinal declined; "Числові назви *тисяча, мільйон, мільярд* відмінюємо як іменники відповідних відмін". §105 does not mention *нуль* |
| 3 | Курс лекцій «Лінгвістичні основи документознавства», ПНТУ ім. Ю. Кондратюка, Полтава 2015 (<https://reposit.nupp.edu.ua/files/original/13/522/b7052be0624c27992e3e22543d6019127bbb49e2.pdf>), p. 132 | **rule**: "Числівники нуль, тисяча, мільйон, мільярд, трильйон, квадрильйон. Числівники цього типу відмінюються як іменники відповідної відміни і групи"; p. 132, on fractions: "У дробових числівниках чисельник відмінюється як відповідний кількісний числівник, а знаменник як порядковий" |
| 4 | Same source, p. 135, "Відмінювання числівників типу нуль цілих сім десятих" | **rule + attested paradigm**: "Якщо десятковий дріб починається на нуль, то частина цілих не відмінюється. В інших випадках вона відмінюється": Н. *нуль цілих сім десятих*, Р. *нуля цілих семи (сімох) десятих*, Д. *нулю (нулеві) цілих семи (сімом) десятим*, З. *нуль цілих сім десятих*, О. *нулем цілих сімома (сьома) десятими*, М. *на нулю (нулеві) цілих семи (сімох) десятих*; against *трьом цілим*, *трьома цілими* |
| 5 | VESUM via Горох, "нуль" (<https://goroh.pp.ua/Словозміна/нуль>) | **paradigm**: masc., II declension, soft group: *нуль, нуля, нулеві / нулю, нуль, нулем, нулю / нулі / нулеві* (cells in this order) |
| 6 | VESUM via Горох, "мільйон", "мільярд", "трильйон", "квадрильйон", "квінтильйон" (<https://goroh.pp.ua/Словозміна/трильйон> …) | **paradigm**: masc., II declension, hard group, identical endings: *-а*, *-ові / -у*, *-ом*, *-і / -ові / -у*; pl. *-и*, *-ів*, *-ам*, *-ами*, *-ах* (cells in this order); slovnyk.ua *трильйон*, *квадрильйон* agree |
| 7 | Brief sources not re-read here: Запорізький національний університет, курс (eir.zp.edu.ua, bitstream bcc33af3…), МДПУ програма 2023–2024, підручник 6 кл. (lib.imzo.gov.ua) | cited by the request for the same class statement and the numerator/denominator rule; #3 states both verbatim and is the one quoted |

**Form policy.** The plugin keeps the first VESUM form of each cell (*мільйонові*, *мільйоні*, already so since NTS-40).
Applied to *нуль*: dative *нулеві* (VESUM *нулеві, нулю*; #4 gives *нулю (нулеві)*, both normative), locative *нулю*
(VESUM *нулю, нулі, нулеві*; #4 *на нулю (нулеві)*). Applied to the generated *-йон* nouns: *-йонові*, *-йоні*.

**E/UK — opened.** *нуль* is a noun (#3, #5). As a noun it governs the genitive plural of what it counts, which then stays
unchanged in every case: #4 states it for *цілих* after *нуль* and contrasts the declined *трьом цілим*, *трьома цілими*.
The fraction denominator after *нуль* is therefore the genitive plural in every case: *нуль третіх*, *нуля третіх*,
*нулеві третіх*, *нуль третіх*, *нулем третіх*, *нулю третіх* — the rule the plugin already applies after *тисяча*,
*мільйон* (*тисячею третіх*, NTS-40). Level: **rule (#1, #3) + attested paradigm (#5) + attested construction with zero
(#4)**; the readings of *0/3*, *0/5* are a **productive application** (no source quotes *нулем третіх*). The construction
proposed in the request (*нулем третіми*, the denominator in the case) contradicts #4 and was not adopted.

**F/UK — opened.** *трильйон*, *квадрильйон*, *квінтильйон* have attested paradigms (#6), identical to *мільйон*; #3 puts
*трильйон*, *квадрильйон* in the same noun class. The UK generated names all end in the configured suffix *йон* after a
Conway prefix ending in *ль*: the case forms are suffix fragments of the UK `<GeneratedScaleForm>` (`singular.dative` =
*йонові* …), read by the plugin through `IScaleNounSource` (NTS-42 F), so the plugin holds no generated name. Level:
**attested** for 10^12–10^18; **productive application by morphological class** above (the prefix invariant, only the
suffix inflected), the extension AGENTS.md allows for Conway names. Multipliers follow §105 unchanged (*одним
трильйоном*, *двома трильйонами*, *двадцятьма одним трильйоном*); the denominator after a numerator ending in a scale
noun stays the genitive plural (*одним трильйоном третіх*).

## NTS-45 — NTS-42 families A, B, D and E for Czech and Slovak (2026-10-21)

Scope: the CS/SK closures NTS-42 kept after F. Old/new outputs and the dump: `DONE-2026-10-21.md`. Evidence levels as in
the NTS-36 section below.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | CS | IJP, "Členění čísel, víceslovné číslovkové výrazy" (<https://prirucka.ujc.cas.cz/?id=791>) | **rule**: every part of a multi-word numeral may be declined, the strategy the CS plugin uses (*před třemi sty šedesáti pěti lety*, *bez tisíce osmi set čtyřiceti sedmi korun*) |
| 2 | CS | IJP, "Počítaný předmět po číslovkách" (<https://prirucka.ujc.cas.cz/?id=792>) | **rule**: after a compound ending in one the noun agrees with the last member (*dvacet jeden žák*) or is in the genitive (*dvacet jedna žáků*, "běžnější"); *nula* quoted as a quantifier in the nominative only (*nula/žádná celá dva metrů*) |
| 3 | CS | J. Žďárek, "Sto jeden žák, či sto jedna žáků", ÚČJTK FF UK, 2009 (ČNK excerpts) | **attested** oblique agreement with a final *jeden*: *dožil se devadesáti jednoho roku*, *po sto jednom roce*, *sto padesát jedním hlasem*; the singular prevails after 101 and 1001 |
| 4 | CS | IJP / ASCS / SSČ / SSJČ, entry *nula* (<https://prirucka.ujc.cas.cz/?slovo=nula>) | *nula* is a feminine noun of the pattern *žena* (*nuly, nule, nulu, nulou*); quantifier examples in the nominative only (*nula celá pět desetin*); **no rule** for *nula* + counted noun in an oblique case |
| 5 | SK | Š. Peciar, "Nula celých, či nula celá?", *Slovenská reč* 23 (1958), 2, 117–118 (<https://www.juls.savba.sk/ediela/sr/1958/2/sr1958-2-lq.pdf>, p. 55 of the PDF) | **rule + attested**: "nesklonnosť výrazu nula v spojení s podst. menom dobre vidno z nepriamych pádov": *ochladiť na nula stupňov* (not *na nulu*), *z nula percent* (not *z nuly*), *s nula celými* (not *s nulou*); like *sto*, *tisíc*, the number stays and the noun "stojí za ním" changes (*pred sto rohmi*) |
| 6 | SK | *Kultúra slova* 36 (2002), 5 (<https://www.juls.savba.sk/ediela/ks/2002/5/ks2002-5.html>) | **rule + attested**: *nula* as a noun declines (*napísať nulu*, *dva stupne nad nulou*), "tvar nula v spojení s počítaným predmetom neskloňuje": *nula stupňov*, *pri nula stupňoch* |
| 7 | SK | Š. Peciar, "O používaní zlomkových výrazov", *Slovenská reč* 41 (1976), 4 (<https://www.juls.savba.sk/ediela/sr/1976/4/sr1976-4-lq.pdf>) | **attested**: *0,5 = nula celých (a) päť desatín*, citing #5; the slash reading in isolation only (*1/3 = jedna lomené troma*, *5/7 = päť lomené siedmimi*, not *lomeno*): nothing on a whole slash phrase under a case (D) |
| 8 | CS | E. Prandstetter, *Naše řeč* 22 (1938), 78–86 | **attested** internal slash structure *X lomeno Y(instr.)* only (see NTS-40/41) |
| 9 | SK | P. Ondrus, *Slovenská reč* 1968, 2; *Slovenská reč* 43 (1978), 6 | **attested** internal slash structure only: *jedna lomené dvoma*, *jeden lomené sto štyridsiatimi dvoma*, *dvanásť lomené štyridsiatimi piatimi* |

**A and B — one rule.** A *jeden/jedna* ending a compound (A: 21, 101, 1001, 10^12 + 1) or a scale multiplier (B: 21 000,
101 000, 31 000 000, 21 × 10^12) is declined with every other part (#1) and agrees with what it counts: the feminine
fraction noun (*jedné*, *jednou*), the gender of the scale noun after a multiplier (*tisíc*, *milion*, *bilion*
masculine: *jednomu tisíci*, *jedním milionem*; *miliarda*, *biliarda* feminine: *jedné miliardě*), the noun then in the
singular of the case (#2 agreement with the last member, attested in the oblique cases by #3). One private helper
(`FinalOne`) serves numerators, multipliers and *lomeno* denominators. Level: **rule + attested construction**; the
fraction-noun combination itself is an **application** of the rule (no source quotes *dvaceti jedné třetiny*). The
nominative and accusative keep the counting form and the genitive plural (*dvacet jedna třetin*, *dvacet jedna tisíc*;
#2, the "běžnější" construction). In *lomeno* the denominator is an instrumental: a multiplier's one agrees with its scale
noun (*jedna lomeno dvaceti jedním tisícem*), a final *jedna* of the whole denominator stays the arithmetic feminine
*jednou* (NTS-41). The inverted *jednadvaceti* (also allowed by #1) is not the configuration's word order and has no
form for 101: not used.

**D — still not established.** Searched again (IJP, ÚJČ poradna database, Naše řeč, Slovenská reč, Kultúra slova
1986–2019 issues, school and university mathematics pages): every source reads the slash phrase in isolation (#7, #8,
#9).
No sentence was found where a whole *X lomeno Y* / *X lomené Y* phrase is governed by a preposition or a case. The
unresolved question is the form of X under an external case: declined (*s dvěma lomeno třemi*, *s dvoma lomené troma*)
or kept as the nominative arithmetic term (*s dvě lomeno třemi*, *s dva lomené troma*). The ordinary cardinal declension
is not evidence for an operator phrase: **fail-closed**.

**E/SK — opened.** #5 and #6 state the rule for a counted noun in the oblique cases; #5 includes the accusative (*na nula
stupňov*: the genitive plural of the direct case). Applied to the fraction nouns with the paradigms already held:
*nula tretín* (genitive, accusative), *nula tretinám*, *nula tretinách*, *nula tretinami*; *nula polovíc*, *nula
poloviciam*. A zero over a denominator without a fraction noun is a slash reading: closed with D.

**E/CS — still closed.** Usage has an indeclinable *nula* (*při nula stupních*), but no Czech normative source states it
(#2, #4): the dictionaries decline *nula* as a noun, and *nulou* by lexical declension would be precisely the analysis #5
rejects for Slovak. Level: **usage attested, not normative** — below the project's threshold.

## NTS-36 and NTS-42 F — Czech and Slovak generated scales (2026-10-20)

Scope: NTS-36 (the spelling of the Conway names CS and SK generate from 10^12) and NTS-42 family F (the CS and SK
oblique fractions and slash readings from 10^12, which need those names in a case). Old/new outputs and the regression
dump: `DONE-2026-10-20.md`. Evidence levels: **attested** (the form itself is in the source), **rule** (a normative
spelling or morphological rule), **productive deduction** (a rule applied mechanically to a class it is established
for, with no contradicting source), **fail-closed** (insufficient evidence; the reading throws). The library's own
output is never evidence.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | CS | IJP (ÚJČ AV ČR), entries *bilion*, *trilion*, *kvadrilion*, *kvintilion*, *sextilion*, *septilion* (<https://prirucka.ujc.cas.cz/?slovo=kvadrilion> …) | **attested** names (*kvadrilion* "bilion bilionů (10^24)", *kvintilion*, *sextilion* "milion kvintilionů (10^36)", *septilion*; each with the variant *-lión*) and their paradigms, m. neživ.: 2. *-u*, 3. *-u*, 4. = 1., 6. *-u* (*septilion* also *septilioně*), 7. *-em*; pl. *-y*, *-ů*, *-ům*, *-y*, *-ech*, *-y* |
| 2 | CS | IJP, entries *biliarda*, *triliarda* | **attested** feminine paradigms (pattern *žena*): *biliardy, biliardě, biliardu, biliardě, biliardou*; pl. *biliardy, biliard, biliardám, biliardy, biliardách, biliardami*; *triliarda* "miliarda bilionů (10^21)" with the same endings |
| 3 | CS | IJP, entries *kvartet*, *kvadragesima*, *oktet*, *sexta*, *atribut* (*z lat.*) | **rule** (spelling of domesticated Latin loans): *qu* → *kv* (*kvartet*, *kvadragesima*), *c* read [k] → *k* (*oktet*), *x* kept (*sexta*), a Latin double consonant simplified (*atribut* < *attributum*); *c* read [ts] kept (*decimetr*, *centimetr*) |
| 4 | CS | IJP: no entry for *oktilion*, *decilion*, *centilion* | the names from 10^48 are not listed: **productive deduction** (AGENTS.md, Conway scale names) |
| 5 | SK | KSSJ 4 (2003), *bilión -a m. čísl. zákl. milión miliónov* (slovnik.aktuality.sk, Krátky slovník) | **attested** *bilión*, a numeral declined like *milión*, genitive singular *-a* |
| 6 | SK | Slovník cudzích slov online (slovnik.aktuality.sk; Ringier Slovakia Media & R. Andraščík, not the academic SCS) | **attested** names without grammatical marks: *biliarda* "tisíc biliónov", *trilión*, *triliarda* "miliarda biliónov", *kvadrilión* "bilión biliónov", *kvadriliarda* "číslo 10^27", *kvintilión* (10^30), *sextilión* (10^36), *septilión* (10^42); *oktilión*, *decilión*, *centilión* not listed. Non-institutional: corroboration of the *kv-* series, not its inflection |
| 7 | SK | KSSJ 4 / PSP 2013, entries *kvadrant*, *kvinta*, *kvinteto*, *oktáva*, *okteto*, *oktán*, *sexta*, *sexteto*, *deciliter*, *centiliter* | **rule** (same spelling of Latin loans as #3): *kv*, *okt*, *x* kept, *c* [ts] kept; a double consonant simplified (*atribút*) |
| 8 | SK | *Morfológia slovenského jazyka* (1966), 101, 329–330 | **rule**: *milión* follows *dub* (*miliónu, milióne, miliónom*; pl. *miliónov, miliónom, miliónoch, miliónmi*), *miliarda* follows *žena*; the genitive singular of an inanimate *dub* noun is lexical (*-a* or *-u*) |

**NTS-36 — the Conway tables.** CS and SK inherited the Latin `SCALE-LONG` tables (*quadrilion*, *quintiliarda*,
*octilion*, *quattuordecilion*). Each language now overrides the four tables (`Scale0Prefixes`, `UnitsPrefixes`,
`TensPrefixes`, `HundredsPrefixes`) with its own spelling of the same components; the Conway construction, its linking
markers and the junction `li` are unchanged, and so are the suffix families and their `forceVariants` (NTS-30C). Every
table differs from `SCALE-LONG` (the units hold *kvatuor*, *kvinkva*, *okto*), so `baseOn` cannot override less than a
whole table; CS and SK each keep their own copy, each sourced separately (#1–#4, #5–#7), rather than a shared base
that would assert their identity.

| Component | SCALE-LONG | CS, SK | Evidence |
|---|---|---|---|
| Scale0 4, 5 | *quadri*, *quinti* | *kvadri*, *kvinti* | **attested** (#1, #6) |
| Scale0 6, 7, 9 | *sexti*, *septi*, *noni* | unchanged | **attested** *sextilion*, *septilion* (#1, #6); *noni* unchanged by the rules |
| Scale0 8 | *octi* | *okti* | **rule** #3, #7 (*oktet*, *oktáva*) — productive deduction for *oktilion* |
| Units 4, 5, 8 | *quattuor*, *quinqua*, *octo* | *kvatuor*, *kvinkva*, *okto* | **rule** #3, #7 (*kv*, *okt*, geminate simplified) — productive deduction |
| Tens 4, 5, 8 | *quadragint*, *quinquagint*, *octogint* | *kvadragint*, *kvinkvagint*, *oktogint* | **rule** #3, #7 (*kvadragesima*) — productive deduction |
| Hundreds 4, 5, 8 | *quadringenti*, *quingenti*, *octingenti* | *kvadringenti*, *kvingenti*, *oktingenti* | **rule** #3, #7 — productive deduction |
| every *c* before *e*/*i*, every *x* | *deci*, *centi*, *ducenti*, *sexagint* … | unchanged | **rule** #3, #7 (*decimetr*, *deciliter*, *sexta*) |

The weakest point is *kvatuor* (10^84 *kvatuordecilion*): no Czech or Slovak form of *quattuor* was found; the
simplified geminate applies the rule of the domesticated Latin loans (*atribut*), which the *kv-* respelling shows these
names to be. Recorded as a productive deduction, not an attestation.

**NTS-42 F — the declined generated nouns.** The paradigms are those of the suffix family, not of a scale: every
attested member of a family shares the same endings (#1, #2 for CS; #5, #8 for SK), the Conway prefix never touches the
ending. The configuration declares them as twelve case keys (`singular.genitive`, `plural.dative` …) on the
`<GeneratedScaleForm>` of the family, beside the count keys; the fraction plugin reads them through the converter, so the
names it declines are the ones the converter writes (no second Conway spelling in the plugins).

- CS: every key of both families: **attested** for *bilion*–*septilion* and *biliarda*, *triliarda* (#1, #2);
  **productive deduction** above. *septilion*'s second locative *septilioně* is not used: *-u* is the only locative of
  every other member.
- SK: the *-ón* family follows *milión* (#5: *bilión*, numeral, "milión miliónov"; #8: pattern *dub*): *biliónu*,
  *bilióne*, *biliónom*; pl. *biliónov*, *biliónom*, *biliónoch*, *biliónmi* — **productive deduction** from the
  attested class; the *-arda* family follows *miliarda* (*žena*, #8) — **productive deduction** as in NTS-30C. The
  genitive singular of the *-ón* family is **fail-closed**: lexical in *dub* (#8), attested for *bilión -a* only (#5);
  no key is declared, so *jedného bilióna tretín* throws rather than guess *-a* for *trilión*. **Opened by NTS-47**
  (*bilión, trilión, kvadrilión, kvintilión -a*, PSP; see the NTS-47 section above).

The construction rules are unchanged: CS declines every part (NTS-40), SK the last component only (NTS-40, NTS-44); a
generated noun stands where *milion*/*milión* stood (singular after one, plural after the other multipliers, a counted
noun in the genitive plural after it). Still closed: NTS-42 A, B (CS *dvacet jedna bilionů* in an oblique case or a
*lomeno* reading, as for *milion*), D and E.

## NTS-40 and NTS-41 — oblique fractions and the large slash readings (2026-10-16)

Scope: NTS-40 (the oblique cases of the UK, CS and SK fractions) and NTS-41 (the CS and SK slash readings beyond the
NTS-39 domain), implemented together because both need declined compound cardinals. One declined-cardinal builder per
language, internal to its `IFractionLanguageSpecifics` plugin (`UkrainianOrdinalLanguageSpecifics.FractionCases.cs`,
`CzechOrdinalLanguageSpecifics.FractionCases.cs`, `SlovakOrdinalLanguageSpecifics.FractionCases.cs`), spells both the
oblique numerators and the slash denominators. `Convert` keeps no case (`Convert(365, "case=…")` is the nominative
cardinal). No XSD, XML or public API change: the declined numerator travels through an `internal` member of
`FractionDenominatorReading` (`WithNumerator`), which `NumberToStringConverter.BuildFractionText` writes instead of the
cardinal pipeline. Old/new outputs: `DONE-2026-10-16(1).md`.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | UK | Український правопис 2019, §107.1 (slovnyk.ua, `prav_par=107`) | **attested**: *двом третім*, *трьома п'ятими*, *ділити на одну двадцяту*; **rule**: fractions decline "як звичайні числівники", the numerator as a cardinal, the denominator as an ordinal |
| 2 | UK | Правопис 2019, §105 (`prav_par=105`; same tables in the 2015 edition, pravopys.net §70) | **attested** paradigms of *один/одна* (*однієї, одній, одну, однією*), *два*, *три*, *чотири* (*чотирма*), *п'ять* … *вісім* (*п'яти, п'ятьма*; *сьома*; *вісьма*), *п'ятдесят* (*п'ятдесяти, п'ятдесятьма*), *двісті* (*двохсот, двомстам, двомастами, двохстах*), *п'ятсот* (*п'ятисот, п'ятистам, п'ятьмастами, п'ятистах*); **rules**: 9–30 as *п'ять* (§105.3); *сорок, дев'яносто, сто* → *-а* (§105.7); **every part of a compound is declined** (§105.6: *з чотирьохсот сімдесяти восьми тисяч шестисот п'ятдесяти дев'яти*); *тисяча, мільйон, мільярд* decline as nouns (§105.11) |
| 3 | UK | Правопис 2019, §106.2 | **rule**: only the last part of a compound ordinal is declined (*вісімдесят восьмого*, *у тисяча сімсот вісімдесят дев'ятому*) |
| 4 | UK | VESUM (goroh.pp.ua) paradigms of *тисяча*, *мільйон*, *мільярд*, *шістсот* … *дев'ятсот*, *третій*, *п'ятий*, *сотий* | **attested** forms the §105 tables abbreviate: *тисячею, тисячам*; *мільйонові, мільйоні, мільйонами*; *шестисот, семистам, вісьмастами*; plural ordinals *третіх, третім, третіми*, *п'ятих, п'ятим, п'ятими* |
| 5 | CS | IJP, "Členění čísel, víceslovné číslovkové výrazy" | **rule**: three strategies for a multi-word numeral: every part (*před třemi sty šedesáti pěti lety*, *bez tisíce osmi set čtyřiceti sedmi korun*), the tens and units only (*o milion tři sta padesát osm tisíc čtyři sta dvaceti třech dokladech*), none (dictation, arithmetic) |
| 6 | CS | E. Prandstetter, "Čtení matematických značek a výrazů", *Naše řeč* 22 (1938), 3, 78–86 | **attested**: *šedesát osm lomeno třemi sty padesáti šesti* (every part of the slash denominator declined), *lomeno jednou celou*, *čtyřmi iks minus jedna*, *em lomeno dvěma en*; **rule**: "zlomkovou čáru čteme nejčastěji lomeno, při čemž jmenovatele čteme v sedmém pádu" |
| 7 | CS | IJP, "Počítaný předmět po číslovkách" | **rule**: 1–4 agree with the noun (*dvě ženy, dvou žen, dvěma ženám*); after 5–99 the noun takes the case in the oblique cases (*pěti ženám, o pěti ženách, s pěti ženami*); after *sto, tisíc, milion, miliarda* the genitive is the basic construction (*tisíce žen, milionu měst*), case agreement possible (*s třemi tisíci diváků/diváky*, *před třemi sty tisíci let/lety*); *k dvěma stům lidem, o dvou stech lidech*; *sto* before another numeral usually undeclined |
| 8 | CS | IJP paradigms *dva*, *tři*, *čtyři*, *sto*, *tisíc*, *milion*, *miliarda*, *dvacet*, *jedenáct*, *devadesát*, *třetina*, *setina*, *tisícina*, *miliontina*, *polovina*; cs.wiktionary *jeden*, *pět* | **attested** case forms (*tří, třem, třech, třemi*; *set, stům, stech, sty*; *tisíci, tisícem, tisícům, tisících*; *milionu, milionem, milionům, milionech*; *miliardě, miliardou, miliardám*; *třetině, třetinou, třetinám, třetinách, třetinami*; *jedné, jednu, jednou*; *jednoho, jedním*; *pěti*) |
| 9 | SK | *Morfológia slovenského jazyka* (1966), 101–105 | **attested** patterns *žena* (*ženy, žene, ženu, ženou*; *ženám, ženách, ženami*; rhythmic law only after a long stem syllable) and *ulica* (*ulice, ulici, ulicu, ulicou*; *uliciam, uliciach, ulicami*) |
| 10 | SK | *Morfológia slovenského jazyka* (1966), 316–330 | **attested** paradigms *jeden* (318: fem. *jednej, jednu, jednou*; masc. *jedného, jednému, jednom, jedným*), *dva* (*dvoch, dvom, dvoma*), *tri* (*troch, trom, troma*), *štyri* (*štyrom, štyrmi*), *päť* (*piatich, piatim, piatimi*); **rules**: after 5+ the noun takes the case in the oblique cases (317: *k desiatim sudom, s deväťdesiatimi piatimi sedadlami*); *dvadsaťdva* declined in both parts (326: *dvadsiatich dvoch, dvadsiatim dvom, s dvadsiatimi dvoma*); the *-jeden* compounds indeclinable, the noun alone declined (326: *ku štyridsaťjeden družstvám, so štyridsaťjeden družstvami*); *sto* undeclined before a noun (327: *k sto robotníkom, so sto robotníkmi*); the *tisíc* compounds usually undeclined (328: *od päťtisíc robotníkov*); *milión* declined after *dub* with its multiplier (329–330: *z jedného milióna, so štyrmi miliónmi, s dvadsiatimi piatimi miliónmi*, or the multiplier undeclined: *s päť, osem, dvadsaťdva miliónmi*), the counted noun always in the genitive plural; *miliarda* after *žena*, "nemá tendenciu k neohybnosti"; in arithmetic the numerals decline (316: *Tristo šesťdesiatpäť násob dvadsiatimi štyrmi*) |

**Strategies chosen.**

- UK — **cardinal declension**: every part (#2, §105.6), the first form of each §105 doublet (*п'яти*, *п'ятьма*,
  *сьома*), the units feminine as in the nominative fraction (*одна, дві*), the scale nouns singular after one and plural
  in the case otherwise, as nouns (§105.11). **Denominator**: the ordinal the plugin already forms, only its last word
  declined (#3): the feminine singular in the case after a numerator read as one (one, or a compound ending in *одна*, as
  NTS-39 does in the nominative: *двадцяти одній сотій* is an **inference** from that agreement), the genitive plural
  after a numerator ending in a scale noun (an **inference** from §105.11: a noun governs the genitive), the plural in
  the case otherwise (#1, #4); the accusative of an inanimate quantity is its nominative (*дві третіх*).
- CS — **cardinal declension**: every part (#5 first strategy, #6), compositional, the strategy NTS-39 already used
  below 1000; *sto* before another numeral undeclined (#7). **Fraction nouns**: their IJP paradigms (#8); 1–4 agree, 5–99
  impose the case (#7), the genitive plural after a numeral ending in *sto*/*tisíc*/*milion*/*miliarda* (#7, the
  basic construction), the accusative = the nominative. **Slash reading (NTS-41)**: *lomeno* + that instrumental up to
  999 999 999 999; a final *jedna* is the feminine *jednou*: arithmetic numbers are feminine (SSČ *jedna a dvě jsou
  tři*; #6 *minus jedna*) and #6 declines it so in a slash denominator (*lomeno jednou celou*) — the composition is an
  **inference** from these attested parts.
- SK — **cardinal declension**: the last component only (Ondrus §8, SR 1978, as in NTS-39), continued through the
  scales (#10): a final *milión*/*miliarda* is declined with its multiplier when the multiplier is one or a declinable
  number below 100 (*jedným miliónom*, *dvoma miliónmi*, *dvadsiatimi dvoma miliónmi*), and keeps an indeclinable or
  larger multiplier in its absolute form (*dvadsaťjeden miliónmi*, *sto štyridsaťdva miliónmi*: #10, second
  construction). **Fraction nouns**: patterns *žena* and *ulica* (#9); 1–4 agree, after 5+ and after the indeclinable
  components the noun takes the case, after *milión*/*miliarda* the genitive plural (#10). **Slash reading (NTS-41)**:
  *lomené* + the instrumental of the last component up to 999 999 999 999 (*jedna lomené dva milióny troma*), *lomené
  číslom* + the cardinal of the configuration when that component is indeclinable, a bare final *jeden* included
  (*jedna lomené číslom jeden milión jeden*). **Bare final *jeden* (NTS-44)**: #10, 226, *vyššie zložené číslovky
  končiace sa na číslovku jeden (podobne ako typ dvadsaťjeden) sú nesklonné*, the millions and milliards counted among
  the higher compounds (note 18, 221; table 2.4.1, 223–224); PSP 1953, 88–89, quoted by #10, 211: *so stojeden
  družstvami*. The noun alone takes the case: *sto jeden tretinám*, *tisíc jeden tretinami* (application of the rule).

**Slash readings under a caller case.** No source declines the *lomeno*/*lomené* phrase as a whole: its denominator is
already the instrumental the construction requires. The caller's case is not piped into it; an oblique case of a
fraction without a fraction noun **fails closed** (NTS-42).

**Remaining closures (NTS-42).** CS: an oblique compound numerator ending in *jedna* (only its nominative is sourced),
a multiplier ending in one (21 000) in *lomeno* or an oblique case. SK: the oblique numerator ending in a bare *jeden*
(101) was opened by NTS-44. UK, CS, SK: a zero numerator in an oblique case (no source). From 10^12: UK still closed;
CS and SK opened by NTS-42 F (see above), but the SK genitive singular of a generated *-ón* noun. Denominator one stays closed
everywhere (NTS-39). BG has no NTS-40 item and is byte-identical.

## NTS-39 — the fraction domains NTS-30H left closed (2026-10-16)

Scope: the six NTS-39 items (compound numerators; CS denominators without a noun; SK nouns; oblique cases; zero
numerators; denominator one), audited item by item. Implementation inside the four `IFractionLanguageSpecifics` plugins
and their shared helpers; no engine, XSD, XML or public API change. Old/new outputs: `DONE-2026-10-16.md`.

Each row says whether the form is **attested** (the example is in the source), follows a **rule** the source states, or
is an **inference** from such a rule.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | UK | І. Літвінова, *Українська мова. 6 клас*, ч. 6 (Ранок, 2022), p. 27 and exercise 11 | the noun after a fraction agrees with the implied *частина*; **attested**: *двадцять дві шістдесятих*, *тридцять чотири сорок третіх* (compounds ending in 2–4: genitive plural) |
| 2 | UK | "Числівник" (teaching sheet, НККЕП), rules 4–5 | **rule**: after a compound the noun agrees with the last component (*тридцять чотири автомобілі*); "якщо … числівник дробової частини виражені одиницею або закінчуються на одиницю, то весь запис треба ставити в називний відмінок"; **attested**: *21,31 — двадцять одна ціла й тридцять одна сота*. Ordinary fractions: **inference** from the same agreement with *частина* (*двадцять одна тридцять сьома*, *сто одна тисячна*) |
| 3 | UK | Український правопис 2019, §107.1 | the oblique cases decline the numerator as a cardinal (*двом третім*, *трьома п'ятими*, *ділити на одну двадцяту*) |
| 4 | BG | Г. Митринов, ИБЕ-БАН, "За една особеност в употребата на числителните имена", *Аз-буки* 13 (2020) | **rule**: a noun after a compound numeral ending in *един* is plural (*двайсет и един футболисти*, *двайсетте и един апарата*) |
| 5 | BG | *Математика 5. клас*, Клет България (2021), lesson 58 "Десетични дроби" | **attested** in a fraction: *едно цяло и сто и една хилядни*; *една стотна*, *двадесет и седем стотни* |
| 6 | CS | IJP (ÚJČ AV ČR), "Počítaný předmět po číslovkách" | **rule**: after compounds ending in *jeden … čtyři* both *dvacet jeden žák / dvacet dva žáci* and *dvacet jedna žáků / dvacet dva žáků* are possible, the genitive "běžnější a přirozenější"; *sto* followed by another numeral usually undeclined (*ve sto dvaceti případech*) |
| 7 | CS | IJP, "Členění čísel, víceslovné číslovkové výrazy a desetinná čísla" | **attested**: *103,8 — sto tři celé/celých*, *1024,007 — tisíc dvacet čtyři celé/celých sedm tisícin*; every part declined (*před třemi sty šedesáti pěti lety*); in arithmetic the expression may stay in the counting form (*k tisíc sedm set dvacet dva korunám*) |
| 8 | CS | IJP dictionary: *třináctina* … *devatenáctina*, *dvacetina*, *třicetina* … *devadesátina*, *stotisícina*, *miliardtina* | **attested** nouns with their paradigms (SSČ "zlomk."); *jednadvacetina*, *dvacetipětina*, *desetitisícina*, *dvousetina* not found: no productive rule for compounds |
| 9 | CS | IJP *lomit* (SSČ *pět lomeno dvěma (5/2)*; SSJČ *dvě lomeno třemi 2/3*); IJP paradigms *dva* (7. *dvěma*), *tři* (*třemi*), *čtyři* (*čtyřmi*), *šest* … *devět*, *deset*, *jedenáct*, *devadesát* (*-i*), *sto* (7. *stem*, pl. *sty*); SSČ *dva*: *jedna a dvě (čast. než jeden a dva) jsou tři* | **rule**: *lomeno* + instrumental; denominators 2–999: **attested** pattern for the simple numerals, the compounds and hundreds by the IJP declension rule (#7) |
| 10 | SK | J. Jarošová, "Zložené číslovky so zreteľom na písanie spolu a oddelene", *Slovenská reč* 86 (2021), 208–229 | **attested**: *dvadsaťdva – pri všetkých substantívach základný tvar (dvadsaťdva žien, detí)*, also for 102, 122, 322; *dvadsaťjeden*, *stojeden* indeclinable; inflected compound in two words: *dvadsiatimi dvoma* |
| 11 | SK | J. Páleníková (JÚĽŠ SAV), "Číslovky dvadsaťjeden, tridsaťjeden, štyridsaťjeden", *Quark* (2026) | **attested**: *21 žien = dvadsaťjeden žien (nie dvadsaťjedna žien)*; *so šesťdesiatimi siedmimi bodkami* |
| 12 | SK | *Morfológia slovenského jazyka* (1966), 317–332 | **attested**: *dvadsaťdva jabĺk*, *dve šestiny a tri šestiny je päť šestín*, *dve polovice, dve tretiny*; **rule**: *nula* governs the genitive plural (*nula stupňov*, *nula celých*); paradigm of *päť* (I. *piatimi*) for 5–99 except the compounds with *-jeden*; I. *štyrmi*; *deväťdesiatimi piatimi*, *dvadsiatimi štyrmi*; **rule** of the fraction nouns: from *pätina* on the cardinal + *-ina* (*jedenástina … dvadsatina, päťdesiatina, tisícina*), *tretina, štvrtina* on the ordinal, *stotina, milióntina, miliardtina* on an extended stem |
| 13 | SK | *Krátky slovník slovenského jazyka* (2003) | **attested** paradigms: *polovica -e -víc*, *štvrtina, pätina, šestina, devätina, desatina, dvadsatina, tridsatina, štyridsatina, stotina -y -tín*, *sedmina, osmina -y -mín*, *dvanástina, šestnástina, sedemnástina, devätnástina, šesťdesiatina, sedemdesiatina, osemdesiatina, milióntina -y -tin*, *tisícina -y -cin*; *dva* I. *dvoma*, *tri* I. *troma*; genitives *šiestich … deväťdesiatich* |
| 14 | SK | P. Ondrus, *Slovenská reč* 33 (1968), 38–41 | **attested**: *jedna lomené dvoma*, *jedna lomené troma*; **rule**: after *lomené* a declinable numeral is in the instrumental; an indeclinable one (*dvadsaťjeden … deväťdesiatjeden*, *sto*, *tisíc*, *päťsto*, *päťtisíc*) takes *číslom* + its absolute form (*jedna lomené číslom dvadsaťjeden*, *jedna lomené číslom sto*); absolute forms *jedna, dva*, not *jeden, dve*; §8: the last component sets the grammar of a compound (*o gramatickom charaktere zloženého slova … rozhoduje vždy posledný komponent*), hence *päťsto*, *päťtisíc* indeclinable; §6: after *číslo* any numeral keeps its absolute form |
| 15 | SK | *Slovenská reč* 43 (1978), 6, p. 44 (on Ondrus's proposal) | **attested**: *jeden lomené sto štyridsiatimi dvoma* (1/142), *dvanásť lomené štyridsiatimi piatimi* (12/45): only the last component is declined, *sto* stays absolute; the numerator of one is *jeden* there, *jedna* in #14 |

**Rule implemented.**

- UK: a compound ending in *одна* (not 11) reads as one: feminine nominative singular ordinal (*двадцять одна тридцять
  сьома*, *сто одна тисячна*); compounds ending in two to four keep the genitive plural (#1).
- BG: the guard is removed; any numerator but one takes the plural (*двадесет и една тридесет и седми*, *сто и една
  хилядни*), the numerator feminine (*една*, as in #5).
- CS: compound numerators (and any numerator but exactly 1–4) govern the genitive plural with the counting form (#6,
  #7: *dvacet dva třetin*); the lexicon is extended to every IJP noun (#8: 2–20, round tens, 100, 1000, 100 000, 10^6,
  10^9); any other denominator from 2 to 999 reads *lomeno* + instrumental, every part declined but *sto* before another
  numeral (#6, #7, #9: *dvě lomeno třemi sty šedesáti pěti*, *pět lomeno sto dvaceti*), numerator 1–4 feminine (*dvě*),
  compound numerators in the counting form (#7).
- SK: compound numerators keep the base form (masculine) and govern the genitive plural (#10–#12: *dvadsaťdva tretín*,
  *dvadsaťjeden tretín*, *sto dva tretín*); the noun lexicon covers the domain of the rule #12 with the paradigms of
  #13 (11, 13, 14, 15, 18, 50, 90 and 10^9 are **inferences**: formed by the rule, genitive plural of their pattern);
  zero takes the genitive plural (*nula tretín*, rule #12); any other denominator up to 999 999 reads *lomené* + the
  instrumental of its last component, the words before it in the absolute form of the configuration (#14, #15: *jedna
  lomené dvadsiatimi troma*, *jedna lomené sto štyridsiatimi dvoma*, *dva lomené tristo šesťdesiatimi piatimi*), or
  *lomené číslom* + the cardinal when that last component is indeclinable (#14: *sto*, *tisíc* and their compounds,
  the *-jeden* compounds: *jedna lomené číslom dvesto*, *jedna lomené číslom dvesto dvadsaťjeden*). A bare final
  *jeden* (*sto jeden*) also takes *číslom*: it is indeclinable like *dvadsaťjeden* (#10, 226; NTS-44) and #14 §6
  admits *číslom* before any numeral. The numerator is in its absolute form (*jedna*, *dva*, #14); #15 reads *jeden* for one: the divergence
  is documented, #14's explicit rule is kept. Extending the last-component rule from 142 to the thousands
  (*tisíc štyridsiatimi dvoma*, *tristo šesťdesiatimi piatimi*) is an **inference** from #14 §8 and #15.

**Zero numerator.** SK: *nula* + genitive plural (rule #12). CS: no source gives *nula* with a fraction noun (the IJP
reads *nula celá* with the singular *celá*), so zero takes *lomeno* (*nula lomeno třemi*): an **inference** from the
productive division reading #9 (no attested example). UK, BG: no source reads a zero numerator (only *нуль цілих*,
*нула цяло* in decimals): **deliberately fail-closed**.

**Denominator one.** No source in any of the four languages reads a written *n/1*; the instrumental of a bare *jedna /
jeden* (CS *lomeno*) is not sourced, and no source reads a Slovak *n/1* with *číslom*:
**deliberately fail-closed** in all four. `ConvertFraction` does not reduce (`2/4` is read as written); `Number`
keeps its own normalization (`Number(5, 1)` is the integer, `Number(2, 4)` is 1/2).

**Oblique cases.** §107 (#3) and the CS/SK paradigms decline the numerator as a cardinal in every case; the UK, CS and
SK cardinals of the configuration have no case declension, so a declined fraction needs a declined-cardinal feature
first: **moved to NTS-40** (since closed, see the NTS-40/41 section above).

**Still closed in CS/SK, moved to NTS-41**: CS denominators ending in *jedna* after the tens or hundreds (21, 101: the
gender of a bare *jedna* in the instrumental is not sourced) and from 1000 without a noun (the IJP gives both the
all-parts and the tens-and-units-only declension for large numbers); SK denominators above 999 999 without a noun
(since opened by NTS-41, see the NTS-40/41 section above).

## NTS-30H — fractions of Ukrainian, Bulgarian, Czech and Slovak (2026-10-15)

Scope: the NTS-30 item "UK, BG, CS, SK `ConvertFraction`", seen by NTS-30B. The connector reading *numerator,
`fractionSeparator`, denominator cardinal* (`один на тисяча`, `едно на хиляда`, `jedna ku tisíc`) is not how these
languages read a fraction; it is replaced by the generic `IFractionLanguageSpecifics` extension point, implemented by
each language's existing `<LanguageSpecifics>`. Old/new outputs: `DONE-2026-10-15(3).md`.

| # | Language | Source | What it establishes |
|---|---|---|---|
| 1 | UK | Український правопис (2019), §107.1 | numerator in the feminine (agreeing with the implied *частина*); denominator the ordinal, feminine nominative singular after *одна* (*одна друга*, *одна третя*, *одна четверта*), genitive plural after any other numerator (*дві третіх*, *три четвертих*, *чотири третіх*, *три п'ятих*, *дві сьомих*, *п'ять других*, *дев'ять десятих*) |
| 2 | BG | Institute for Bulgarian Language (ИБЕ-БАН), language advice on fractions; P. Kancheva (2016) | numerator in the feminine (*една*, *две*, agreeing with *част*); denominator the ordinal, feminine singular after one (*една втора*, *една осма*), plural otherwise (*две трети*, *три четвърти*, *седем осми*); one quarter is the noun *една четвърт* |
| 3 | CS | Internetová jazyková příručka (ÚJČ AV ČR), fraction nouns and their paradigms | the ordinary reading is a feminine fraction noun: 1 → nominative singular, 2–4 → nominative plural, 5+ → genitive plural (*jedna třetina*, *dvě třetiny*, *pět třetin*; *polovina*, *čtvrtina* … *dvanáctina*, *setina*, *tisícina*, *miliontina*); no fraction reading with *ku* (its IJP entry lists only *ku podivu*, *ku příkladu*); the mathematical reading *lomeno* + instrumental (IJP *lomit*: *dvě lomeno třemi* 2/3) |
| 4 | SK | P. Ondrus, *Slovenská reč* 33 (1968), 40–41; Krátky slovník slovenského jazyka; Pravidlá slovenského pravopisu | the everyday and arithmetic reading with fraction nouns (*jedna polovica*, *jedna tretina*, *pätina*, *jedna stotina*, *jedna tisícina*); paradigm *tretina -y -tín*; *ku* reads a ratio (*dvadsaťsedem má sa ku deviatim*); specialist reading *lomené* + instrumental |

**Rule implemented.** UK and BG: the numerator is the cardinal forced to the feminine, the denominator the ordinal the
plugin already forms, in the form of #1/#2 (productive beyond the attested denominators: *одна тисячна*, *дві
двадцять п'ятих*, *една стотна*, *една сто и първа*). CS and SK: closed lexicons of the fraction nouns of #3/#4,
inflected by the numerator class one / two to four / five and more; a form the sources do not give (Slovak plurals
other than *tretina*) is absent.

**Fail closed** (`NotSupportedException`, no fallback to the connector): compound numerators ending in *один/една*
after the tens (21, 101: their agreement with the denominator is not sourced) in UK and BG, in 1–4 in CS and SK;
zero numerators; denominators the ordinal plugin declines (UK, BG) or without a sourced noun (CS, SK: *1/13*,
*1/101*); a requested case other than the nominative (UK, CS, SK). The productive CS/SK *lomeno/lomené* +
instrumental reading needs a cardinal instrumental the configurations do not have; it is recorded in the TODO.

## NTS-30G — Basque final *eta* and the Spanish masculine million multiplier (2026-10-15)

Scope: the two NTS-30 items opened by NTS-30F, closed by two independent generic primitives (`finalGroupConnector`,
and `forceVariants` on the forms of an exact `<ScaleForm>`). Old/new outputs and the regression dump:
`DONE-2026-10-15(2).md`. The Euskaltzaindia rules were read from their PDFs (text layer), the RAE entries in full on
2026-10-15 (rae.es and dle.rae.es, fetched with `curl.exe`).

### EU — placement of *eta* between groups

| # | Source | What it establishes |
|---|---|---|
| 1 | Euskaltzaindia, rule 203 (2025) §4 (<https://www.euskaltzaindia.eus/dok/arauak/Araua_0203.pdf>): "Mila zenbakitik aurrera, *eta* kenduko da ondoren datozen ehunekoetan ere agertzen bada; bestela, jarri egin behar da: (egokia) *mila eta hiru; mila eta hamaika; mila eta berrogeita sei* […] *mila eta ehun; mila eta berrehun* […] *bost mila eta berrogei; bi mila eta hogeita bost* […] *mila ehun eta zazpi; mila hirurehun eta hogeita bost*; (desegokia) *mila eta ehun eta zazpi*" | from a thousand upwards an *eta* is placed before the following part, and **dropped when the hundreds of that part carry their own** *eta* |
| 2 | Rule 203 §4, table: "1 200 000 *milioi bat eta berrehun mila*", "1 647 413 *milioi bat seiehun eta berrogeita zazpi mila laurehun eta hamahiru*", "2 700 000 *bi milioi eta zazpiehun mila*", "**60 200 001 *hirurogei milioi berrehun mila eta bat***", "500 321 207 *bostehun milioi hirurehun eta hogeita bat mila berrehun eta zazpi*" | the *eta* of #1 stands **only before the last group** of the number: 60 200 001 has none after *milioi* although *berrehun mila* carries no *eta* of its own; 2 700 000 and 1 200 000 have one because *zazpiehun mila* / *berrehun mila* is the last group |
| 3 | Rule 203 §4: "(egokia) 206 795 843 237: *berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila berrehun eta hogeita zazpi*"; "(desegokia) … *milioi, zortziehun eta berrogeita hiru mila* …" | the six-digit multiplier of *milioi* places its own *eta* (*berrehun eta sei mila zazpiehun eta …*): a multiplier is a numeral phrase of its own; no *eta* after *milioi* before a non-final group |
| 4 | Euskaltzaindia, rule 7 (1994) (<https://www.euskaltzaindia.eus/dok/arauak/Araua_0007.pdf>), point 7 and table: "Mila zenbakitik gorakoen artean ere ohiturak ez dira berdinak, eta juntagailua sartu ala ez: *mila Ø bortz ehun eta berrogoi* […] *milla eta ehun gizon*"; "1200 *mila eta berrehun*", "1201 *mila berrehun eta bat*" | the same rule in the older text (usage split on the conjunction, decided by the table); rule 203 supersedes it with the large-number examples of #2 |

**Rule implemented.** One *eta* per numeral phrase, at the junction before its last non-zero group (#1–#3); the
phrase is the whole number, or a multiplier of *milioi* spanning two groups (`pairedScalesFrom="2"`, NTS-30F), which
places its own. The generic `finalGroupConnector` (inter-group assembly only) expresses it; the *eta* inside a group
stays in the `buildString`s (*ehun eta bat*, *berrehun eta hogeita bost*), and the existing `eta X-ehun eta → X-ehun
eta` replacements implement the "dropped when the hundreds carry their own" clause (*mila berrehun eta bat*,
*… mila laurehun eta hamahiru*). `groupConnector="eta"` (every junction, threshold 1000) is removed: it put an *eta*
after *milioi* before a non-final group. `finalComponentConnector` (NTS-30D) was not reusable: it also joins the
components inside a group, where EU already writes its *eta* in the `buildString`s.

Internal vs. final *eta*, 2 500 001 001: *bi mila **eta** bostehun* (the multiplier's own phrase) *milioi mila **eta**
bat* (the whole number's last group); 2 500 001 000: *bi mila eta bostehun milioi eta mila* (*mila* is the last group).

Also removed: the historical replacement `bat ehun → ehun` (`scope="Anywhere"`). A group never spells its hundreds
*bat ehun* (the digit table gives *ehun*), so the rule could only meet the junction of *milioi bat* with a following
*ehun …* group, where it deleted the multiplier; the removed *eta* had been hiding it. 1 100 001 is *milioi bat ehun
mila eta bat*, like #2's *milioi bat seiehun …*.

**Attested vs. productive.** Attested: the #1 and #2 values (1001, 1100, 1107, 1200, 1201, 1984, 2025, 5040,
13 425, 147 589, 1 000 000, 1 200 000, 1 647 413, 2 700 000, 10 000 000, 60 200 001, 500 000 000, 500 321 207) and the
#3 model number. Productive (#1–#3 composed): *milioi bat eta bat*, *bi milioi eta mila*, *mila milioi eta bat*,
*bi mila eta bostehun milioi mila eta bat*, *milioi bat ehun mila eta bat*.

### ES — the multiplier of *millón* is masculine

| # | Source | What it establishes |
|---|---|---|
| 5 | RAE, DLE "millón" (<https://dle.rae.es/millón>): "1. **m.** Mil millares. *Un millón de euros, de habitantes*." | *millón* is a masculine noun |
| 6 | RAE/ASALE, DPD "cardinales" §3 (<https://www.rae.es/dpd/cardinales>): "Los cardinales, cuando son sustantivos, son siempre masculinos: *el tres, el ciento veinticinco, un millón*"; "El género del numeral lo determina el sustantivo al que se refiere"; "En el caso de las centenas, el cardinal en función determinativa o adjetiva debe concordar necesariamente en género con el sustantivo al que cuantifica, tanto si lo precede inmediatamente (*doscientos kilos, las trescientas toneladas*) como si entre ellos se interpone otro elemento, por ejemplo, la palabra *mil* […] (*doscientos mil kilos, las trescientas mil toneladas*)" | the hundreds agree with **the noun they quantify**, and agreement crosses *mil* |
| 7 | DPD "cardinales" §6: "Las voces *millar, millón, millardo, billón* […] son sustantivos […] la noción cuantificada se expresa en un complemento encabezado por la preposición *de*: *un millón de personas, dos billones de pesos*; ahora bien, cuando van seguidos de otros cardinales formando parte de un numeral complejo, el sustantivo que expresa la entidad cuantificada no va precedido de preposición: *un millón doscientas mil personas*" | the multiplier of *millones* quantifies *millones*, not the counted noun (which is a complement with *de*); **the groups after the million phrase quantify the counted noun** (*doscientas mil personas*) |
| 8 | DPD "uno" §2.3 (<https://www.rae.es/dpd/uno>): "Los numerales *uno, una* y sus compuestos concuerdan en género con el sustantivo al que acompañan cuando lo preceden inmediatamente"; "la concordancia en femenino se está imponiendo […] por analogía con la que obligatoriamente establecen los numerales referidos a centenas: *setecientas mil toneladas* (no ⊗ *setecientos mil toneladas*)" | the same agreement for *uno* (the apocope before *millón* stays masculine: *veintiún millones*); the feminine before *mil* is the counted noun's, as kept for the lower groups |

**Rule implemented.** Derived, not stated in so many words: no consulted entry prints *quinientos millones de
personas*; the conclusion follows from #5 (masculine noun) and #6 (the hundreds agree with the noun they quantify),
with #7 placing the counted noun after *de*. So the multiplier of *millón/millones* — one group or a paired six-digit
multiplier — is masculine whatever the caller's gender (*quinientos millones*, *dos mil quinientos millones*,
*quinientos mil quinientos millones*), while the lower groups keep the caller's feminine (*quinientos millones
quinientas mil quinientas*, *un millón doscientas mil*). Configured by `forceVariants="gender=masculino"` on the
`singular` and `plural` forms of `<ScaleForm scale="2">` (the synthesized words *millón*/*millones* restated), which
forces only the gender: the `form` dimension (attributive apocope) stays the caller's. The NTS-27/NTS-30F apocope
(*un millón*, *veintiún millones*, *mil un millones*) is unaffected.

## NTS-30F — Spanish and Basque million multipliers of four to six digits (2026-10-15)

Scope: the ES and EU cardinals from 10^9 to the unchanged `maxNumber` 999 999 999 999, which composed the milliard as
an independent scale (`dos mil millones quinientos millones`, `bi mila milioi eta bostehun milioi`). Old/new outputs
and the regression dump: `DONE-2026-10-15(1).md`. The DPD entries were read in full on 2026-10-15 (rae.es, 2nd
edition); the Euskaltzaindia rules from their PDFs (text layer extracted with PyMuPDF).

| # | Source | What it establishes |
|---|---|---|
| 1 | RAE/ASALE, DPD "números" §3.3b–c (<https://www.rae.es/dpd/números>): "*327 millones de habitantes*"; "no debe escribirse ⊗ *13 mil millones de dólares* […] deben escribirse enteramente en cifras: *125 000 personas*, *13 000 millones*" | the quantity before *millones* is one numeral of up to six digits (13 000 = *trece mil*): **the multiplier of *millón* is a complete cardinal** |
| 2 | RAE/ASALE, DPD "millardo" (<https://www.rae.es/dpd/millardo>): "'mil millones (10^9)' […] en la lengua general resulta más natural y de inmediata comprensión el uso de la expresión *mil millones*: «Puede pagar mil millones, dos mil millones»"; DPD "billón": "'un millón de millones (10^12)' […] no se recomienda su empleo en español con el sentido de 'mil millones'" | long scale; 10^9 is ***mil millones*** (not a scale noun of its own: *millardo* is a recommended alternative, not the default), *dos mil millones* |
| 3 | RAE/ASALE, DPD "cardinales" §1 (table) (<https://www.rae.es/dpd/cardinales>): "1001 mil uno, fem. mil una; apocopado: mil un", "1021 … apocopado: mil veintiún", "1031 … apocopado: mil treinta y un", "1101 … apocopado: mil ciento un", "21 000 veintiún mil", "31 000 treinta y un mil", "1 000 000 un millón", "2 000 000 dos millones", "1 000 000 000 mil millones o un millardo", "1 000 000 000 000 un billón" | the apocopated form of every multiplier ending in one; *mil* alone for 1000 |
| 4 | DPD "cardinales" §4: "Cuando el cardinal *uno* y sus compuestos se anteponen, como determinantes, a un sustantivo masculino, adoptan siempre la forma apocopada *un*: *un libro, veintiún soldados, ciento un opositores*"; §6: *millón* … "son sustantivos"; DPD "uno" §2.2: "Estas mismas leyes de la apócope rigen cuando *uno, una* es componente de numerales complejos, escritos en una o en varias palabras" | *millones* is a masculine noun: a multiplier ending in one is apocopated before it (*mil un millones*, *veintiún mil un millones*, *treinta y un mil un millones*), and before *mil* inside the multiplier (*veintiún mil millones*, *ciento un mil millones*) |
| 5 | Euskaltzaindia, rule 7 *Zenbakien idazkeraz* (1994) (<https://www.euskaltzaindia.eus/dok/arauak/Araua_0007.pdf>), table: "1000 mila", "1200 mila eta berrehun", "1201 mila berrehun eta bat", "1.000.000 milioi bat", "10.000.000 hamar milioi", "1.000.000.000 mila milioi (miliar bat)"; note: "Mila zenbakitik gora eta kenduko da ondoren datozen ehunekoetan ere agertzen bada" | *milioi bat* (one after the noun) only for one million; ***mila milioi*** for 10^9; the *eta* after *mila* is dropped when the following hundreds carry their own |
| 6 | Euskaltzaindia, rule 203 *Euskara Batuaren Ortotipografia: zenbakizko adierazpenak* (approved 2025-10-31 Donostia, 2025-12-19 Baiona) (<https://www.euskaltzaindia.eus/dok/arauak/Araua_0203.pdf>), §4.1: "Euskaraz, eskala luzea erabiltzen da, gaztelaniaz eta frantsesez bezala"; table "10^9 = 1 000 000 000 mila milioi / miliar bat", "10^12 bilioi bat", "10^15 mila bilioi" | Basque uses the **long scale**; 10^9 is *mila milioi*, 10^15 *mila bilioi* (the same pairing above the billion, not configured here: `maxNumber` unchanged) |
| 7 | Rule 203 §4, table and examples: "1001 mila eta bat", "1 200 000 milioi bat eta berrehun mila", "2 700 000 bi milioi eta zazpiehun mila", "500 321 207 bostehun milioi hirurehun eta hogeita bat mila berrehun eta zazpi", "(egokia) 206 795 843 237: berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila berrehun eta hogeita zazpi"; "(egokia) mila eta ehun; mila eta berrehun […] bi mila eta hogeita bost", "(desegokia) mila eta ehun eta zazpi" | **a six-digit multiplier of *milioi*, attested**, with the *eta* of its own thousands placed by the general rule; no *eta* is added between the multiplier and *milioi* |
| 8 | Rule 203, note 17: "*milioi bat euro* da molde estandarra, eta ez *milioi euro*" | the noun-before-*bat* order belongs to the one million, not to a multiplier ending in one |

**Rules implemented.**

- ES and EU: `pairedScalesFrom="2"` (the NTS-37 primitive, unchanged): scale 2 (*millón*, *milioi*) takes the group
  above it into its multiplier, rendered as a complete cardinal; scale 3 has no name of its own (`CanNameGroup(3)` is
  false, `GetScaleName(3)` throws). *mil millones* / *mila milioi* are no longer stored: they are *mil* / *mila* (the
  existing `onScale="1" onValue="1"` rules) followed by the scale noun (#2, #5).
- ES (#1–#4): the existing NTS-27 apocope rules apply inside the multiplier (scale 1) and before *millones* (scale 2):
  *mil un millones*, *mil veintiún millones*, *veintiún mil un millones*, *ciento veintiún mil ciento veintiún
  millones*. Removed: the scale-3 literal *mil millones* and its rules `uno mil millones → mil millones` (onValue 1),
  `uno mil millones → un mil millones`, and scale 3 from `veintiuno mil → veintiún mil` (`onScale="1,3"` → `"1"`).
- EU (#5–#8): the multiplier keeps its own *eta* (*mila eta bostehun milioi*, *bi mila bostehun eta bat milioi*);
  `bat milioi → milioi bat` keeps `onValue="1"`, so *mila eta bat milioi* is not reordered. Removed: the scale-3
  literal *mila milioi* and its rule `bat mila milioi → mila milioi`.

**Attested vs. productive.** Directly attested: ES *mil millones*, *dos mil millones* (#2), the multiplier *13 000*
(#1), the apocopated multipliers 1001, 1021, 1031, 1101, 21 000, 31 000 (#3); EU *milioi bat*, *hamar milioi*,
*mila milioi* (#5, #6) and *berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita
hiru mila* (#7). Productive (the attested rules composed, no counter-example found): ES *dos mil quinientos
millones*, *mil un millones*, *veintiún mil un millones*; EU *mila eta bostehun milioi*, *bi mila eta bostehun
milioi*, *mila eta bat milioi*, *bi mila eta bat milioi*. Rule 203's example #7 writes 237 as *berrehun eta hogeita
zazpi* (227): the tests pin it up to *… hiru mila*, before that inconsistency.

**Found, not fixed (recorded in `TODO.md`).**

- EU: an *eta* is written after *milioi* before a non-final component (`hirurogei milioi eta berrehun mila eta bat`),
  whereas rule 203 writes *hirurogei milioi berrehun mila eta bat* (60 200 001); the same holds above 10^9
  (`… milioi eta mila eta bat`). Pre-existing below 10^9, not changed by NTS-30F. Fixed by NTS-30G.
- EU ordinal of 1 000 000: `milioi batgarren`, whereas rule 203 §5 gives *milioigarren* (and *milioi bat eta
  batgarren* for 1 000 001).
- ES feminine: the hundreds of a multiplier of *millones* agree with the counted noun (`quinientas millones`,
  `dos mil quinientas millones`), whereas *millón* is a masculine noun (DPD "cardinales" §6); the concord before
  *millones* with a feminine counted noun still needs a source. Pre-existing below 10^9. Fixed by NTS-30G.

## NTS-37 — Finnish large numbers, ordinals to 1 999 999, cases of zero (2026-10-15)

Scope: the Finnish cardinals above 999 999 (scale names, agreement, cases), the ordinals from 2000 and the
inflection of *nolla*, through the generic `NumberScale pairedScalesFrom` primitive. Old/new outputs and the
regression dump: `DONE-2026-10-15.md`. All entries were read on 2026-10-14 (the dictionary through its public
search API, the guide pages in full).

| # | Source | What it establishes |
|---|---|---|
| 1 | Kielitoimiston sanakirja (<https://www.kielitoimistonsanakirja.fi/>), entries *miljoona* ("tuhat tuhatta", type 10), *miljardi* ("tuhat miljoonaa", type 5), *biljoona* ("miljoona miljoonaa eli tuhat miljardia"), *triljoona* ("miljoona biljoonaa"), *kvadriljoona* ("miljoona triljoonaa (10^24)"), *kvintiljoona* ("miljoona kvadriljoonaa (10^30)"); *biljardi* only as the cue game; no entry *triljardi*, *kvadriljardi* | Finnish follows the **long scale**, with *miljardi* but **no *-ljardi* name above it**: 10^15 and 10^21 have no name of their own |
| 2 | Same dictionary, examples and paradigms: "Suomessa on noin viisi miljoonaa asukasta", "Hän peri miljoona (harv. miljoonan) markkaa", "miljardi euroa", "Kaksimiljardia kolmemiljoonaa ruplaa"; yks. gen. *miljoonan*, *miljardin*, *biljoonan* …, yks. part. *miljoonaa*, *miljardia*, *biljoonaa* … | the bare noun after one, which is **not said** (*miljoona*, not *yksi miljoona*); the singular partitive after any other multiplier; genitive *-joonan*, *-jardin* |
| 3 | fi.wikipedia, "Suurten lukujen nimet": "10^6 = miljoona, 10^9 = miljardi / tuhat miljoonaa, 10^12 = biljoona, 10^15 = tuhat biljoonaa, 10^18 = triljoona"; *biljardi*, *triljardi* "eivät ole kuitenkaan normien mukaisia eivätkä yleisesti käytössä" (citing Korpela, *Pienehkö sivistyssanakirja*); higher names *sekstiljoona*, *septiljoona*, *oktiljoona* (Otavan tietokalenteri 1970, p. 171), *noniljoona* (Otavan uusi sivistyssanakirja) or *noviljoona*, *dekiljoona* (Antero Vipunen, WSOY 1984, p. 435) | 10^15 = ***tuhat biljoonaa***: the multiplier of *biljoona* spans six digits; the Conway prefixes are written the Finnish way (*kv*, *ks*, *k*) |
| 4 | fi.wiktionary, *desiljoona*, *sentiljoona*, *noniljoona* | *desiljoona* (10^60) and *sentiljoona* (10^600) in the Finnish spelling of the Latin loans (*desi-*, *sentti*) |
| 5 | Kielitoimiston ohjepankki, "Luvut ja numerot: pitkät luvut ääneen luettaessa" (<https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot-pitkat-luvut-aaneen-luettaessa/>): "3 760. [kolmastuhannes seitsemässadaskuudeskymmenes ~ …]", "3 400. [kolmastuhannes neljässadas]", "29 153:een kahteenkymmeneenyhdeksääntuhanteen sataanviiteenkymmeneenkolmeen"; "Luvun kaikkien osien taivuttaminen on kuitenkin aina tyylikkäämpää" | ordinals above 2000: the marker in **every part**, the thousands part one word (*kolmastuhannes*), **one space between the groups**; the cases of long numbers inflect every component, group by group |
| 6 | Kielitoimiston ohjepankki, "Luvut ja numerot: järjestyslukujen taivuttaminen" (<https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot-jarjestyslukujen-taivuttaminen/>): *500:s viidessadas*, *1 000:s tuhannes*, *1 000 000:s miljoonas* | *miljoonas* (10^6); no ordinal of *kaksi miljoonaa* nor of *miljardi* is given |
| 7 | Kielitoimiston sanakirja, *yhdes* and *kahdes*: "järjestysluku suuremman luvun osana sanaa 'ensimmäinen' / 'toinen' vastaamassa"; *tuhannes*, *miljoonas*, *nollas* (type 45); no entry *miljardis*, *biljoonas*, *kahdestuhannes* | *yhdes*/*kahdes* inside a larger number (*kahdestuhannes*, *kahdeskymmenesyhdestuhannes*); *ensimmäinen*/*toinen* as a whole number and as the final part (ohjepankki "järjestysluvut": *kahdeskymmenesensimmäinen*, "tavallisempia ovat kuitenkin vaihtoehdot") |
| 8 | Kielitoimiston sanakirja, *nolla* (type 10): yks. gen. *nollan*, yks. part. *nollaa*; "Miljoonassa on kuusi nollaa" | the zero inflects: partitive *nollaa*, genitive *nollan* |

**Rules implemented.**

- `baseOn="SCALE-LONG"`, `maxNumber` removed: the cardinals are unbounded. Suffixes `joona` and `jardi` after the
  junction `suffixSeparator="l"` (*mi-l-joona*, *mi-l-jardi*); every Conway table overridden in Finnish spelling
  (#3, #4): *kvadri*, *kvinti*, *seksti*, *okti*, *noni*, *kvattuor*, *kvinkva*, *se(ks,s)*, *(n)desi*,
  *(n,ks,s)senti* … Where usage hesitates (*desi-*/*deki-*, *noni-*/*novi-*), the orthographic rule of the Latin
  loans decides.
- `pairedScalesFrom="4"` (generic, see the README): only *biljoona* (4), *triljoona* (6), *kvadriljoona* (8) … are
  named above *miljardi*; their multiplier spans two groups and is a complete cardinal (#1, #3): *tuhat biljoonaa*,
  *kaksituhatta kolme biljoonaa*, *triljoona tuhat biljoonaa*.
- Agreement (#2): `<GeneratedScaleForm>` per suffix family (*joona*/*joonaa*, *jardi*/*jardia*); the one is
  dropped by a `groupWithScale` trigger matching a whole multiplier *yksi* followed by the scale noun alone, so
  *kaksikymmentäyksi miljoonaa* and *tuhat yksi biljoonaa* keep their one. `multiplierSeparator=" "` from NTS-30E
  (*kaksi miljoonaa*).
- Cases (#2, #5): the `Anywhere` replacements of NTS-30E, generalized to the suffix fragments (*-joonaa* →
  *-joona* → *-joonan*/*-joonaa*, same for *-jardi*), cover every scale noun, generated ones included:
  *kahden miljoonan*, *kahta miljoonaa*, *kahdenkymmenenyhden biljoonan*. Zero (#8): `Standalone` *nollaa*, *nollan*.
- Ordinals (#5, #6, #7): ordered `Anywhere` ordinal replacements turn every component of the cardinal into its
  ordinal (*sataa*/*sata* → *sadas*, *tuhatta*/*tuhat* → *tuhannes*, *miljoona* → *miljoonas*, *-kymmentä* →
  *-kymmenes*, *yksi* → *yhdes* …), replacing the former per-word rules; `<OrdinalComposition>` 1001–999 999 by
  1000 and 1 000 001–1 999 999 by 1 000 000 with the separator `" "`, so the final part is composed on its own
  (*ensimmäinen*, *toinen*): *kahdestuhannes*, *kolmastuhannes seitsemässadaskuudeskymmenes*,
  *miljoonas ensimmäinen*.

**Corrected legacy outputs.** 1001–1999 were soldered (`tuhannesensimmäinen`, NTS-28): #5 separates the groups
(*tuhannes ensimmäinen*). Every case of zero kept *nolla*.

**Deliberately fail-closed (not TODO).** The ordinals from 2 000 000 (no source gives the ordinal of *kaksi
miljoonaa*, whose noun stands apart), of *miljardi* and above (*miljardis*, *-ljoonas*: no dictionary entry), and
every non-nominative ordinal (#5 gives a few inflected ordinals, not their paradigms).

## NTS-30E — Finnish cardinal composition and thousand agreement (2026-10-14)

Scope: the spelling of every Finnish cardinal of the supported domain (0–999 999) in the nominative, partitive and
genitive, through the generic multiplier/scale separator (`NumberScale multiplierSeparator`, overridden by
`<ScaleForm multiplierSeparator="…">`). Old/new outputs and the regression dump: `DONE-2026-10-14(3).md`.
All pages were read in full on 2026-10-14.

| # | Source | What it establishes |
|---|---|---|
| 1 | Kielitoimiston ohjepankki, "Luvut ja numerot: numeroiden ryhmittely" (<https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot-numeroiden-ryhmittely/>): "Kun luku esitetään kirjoitettuna sanana, se jaetaan yleensä osiin samoista kohdista kuin numeroin merkittäessä. Tuhatta pienempi osa kirjoitetaan siis yhdeksi sanaksi, samoin tuhansia osoittava osa, miljoonia osoittava osa jne." Examples *tuhat viisisataa metriä (1 500 m)*, *neljätoistatuhatta kuusisataa euroa (14 600 e)*, *viisimiljoonaa neljäsataakaksikymmentäkuusituhatta kuusisataaseitsemänkymmentäneljä henkilöä (5 426 674 hlöä)*; summary: *kaksimiljoonaa neljäsataakaksikymmentäyhdeksäntuhatta viisisataa* | the part below a thousand is **one word** (*kuusisataaseitsemänkymmentäneljä*); the part counting the thousands is one word, multiplier and *tuhatta* together (*neljätoistatuhatta*, *neljäsataakaksikymmentäkuusituhatta*); **one space between the groups**; *tuhat* alone for 1000 (*tuhat viisisataa*) |
| 2 | Same page: "Myös pitkät luvut voi kuitenkin kirjoitettuina sanoina periaatteessa kirjoittaa yhteen, koska ne ovat yhdyssanoja (*viisitoista, viisituhatta*). Selvyyden vuoksi pitkät luvut kannattaa kuitenkin esittää ryhmitellen." | writing everything together is tolerated, grouping is the recommended form: the converter writes the recommended grouping |
| 3 | Same page: "Tasaluvut *miljoona*, *miljardi* jne. kirjoitetaan tavallisesti erilleen edellä olevasta luvusta, varsinkin kun miljoonia tai miljardeja on yli kymmenen: *kolme miljoonaa (~ kolmemiljoonaa)*, *neljäkymmentäyhdeksän miljoonaa*, *sata miljoonaa*, *kymmenen miljardia*" | from the millions the scale noun is **usually separate** from its multiplier, while the thousands are joined (#1): one language-wide multiplier/scale junction cannot express Finnish, hence the per-scale override |
| 4 | Kielitoimiston ohjepankki, "Luvut ja numerot" (<https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot/>): numbers written as words *kaksi*, *seitsemäs*, *sadas*, *satatuhatta* | *satatuhatta* (100 000) |
| 5 | Kielitoimiston ohjepankki, "Luvut ja numerot: peruslukujen taivuttaminen" (<https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot-peruslukujen-taivuttaminen/>), tables "Taivutuspäätteen liittäminen peruslukuihin 1–10 / 11–20 / eräisiin muihin peruslukuihin": *20:n kahdenkymmenen*, *20:tä kahtakymmentä*, *21:n kahdenkymmenenyhden*, *21:tä kahtakymmentäyhtä*, *22:n kahdenkymmenenkahden*, *22:ta kahtakymmentäkahta*, *125:n sadankahdenkymmenenviiden*, *125:tä sataakahtakymmentäviittä*, *3 000:n kolmentuhannen*, *3 000:ta kolmeatuhatta*; *12:n kahdentoista*, *17:n seitsemäntoista*, *17:ää seitsemäätoista*; running text *115:tä sataaviittätoista*, *523:a viittäsataakahtakymmentäkolmea*, *8 564:stä kahdeksastatuhannesta viidestäsadastakuudestakymmenestäneljästä*; "luvuissa 11–19 jälkiosa -toista on taipumaton kaikissa sijamuodoissa" | **every component inflects**, the compound stays one word; *-toista* never inflects; the multiplied *-kymmentä*, *-sataa*, *-tuhatta* are already partitive; *7*, *8*, *9* have the same nominative and genitive (*seitsemän*, *kahdeksan*, *yhdeksän*) |
| 6 | Kielitoimiston ohjepankki, "Luvut ja numerot: järjestysluvut" and "… järjestyslukujen taivuttaminen" (NTS-28) | ordinal forms up to *tuhannes* (1000); **no round-thousand ordinal above 1000** (2000th, 3000th, 21 000th) is given |

**Rules implemented.**

- Inside a group: the hundreds and tens `buildString` carry no space (`kaksikymmentä*`, `sata*`): *kaksikymmentäyksi*,
  *satayksi*, *satakaksikymmentäyksi*, *yhdeksänsataayhdeksänkymmentäyhdeksän* (#1).
- Thousands: `<ScaleForm scale="1" multiplierSeparator="">` with *tuhat* (singular, multiplier one) and *tuhatta*
  (after any other multiplier): *kaksituhatta*, *kymmenentuhatta*, *yksitoistatuhatta*, *kaksikymmentäyksituhatta*,
  *satatuhatta*, *satakaksikymmentäyksituhatta* (#1, #2, #4). The elided one stays a scale-1 replacement on the whole
  group, now `yksituhat → tuhat` (same mechanism as Hungarian `egyezer → ezer`): the default selector already picks
  *tuhat* for one, so only the multiplier has to go.
- Between groups: `groupSeparator=""` plus `separator=" "`, one space (*kaksikymmentäyksituhatta
  satakaksikymmentäyksi*) (#1). The language `separator` stays a space and is **not** emptied: it also separates the
  words of decimals (*kaksikymmentäyksi pilkku viisi*), fractions (*kaksikymmentäyksi yli satayksi*), currencies and
  digit-by-digit readings, none of which is a compound.
- Millions: `NumberScale multiplierSeparator=" "` (#3). The domain stays `maxNumber="999999"`, so this default is not
  reachable today; the `miljoona` suffix and its variant rules predate the restriction and are kept as dormant
  configuration for a future opening of the millions, which needs its own audit (*miljoona*/*miljoonaa*,
  *kaksi miljoonaa*, *kaksikymmentäyksi miljoonaa*).
- Cases (#5): every nominative component is replaced wherever it occurs (`scope="Anywhere"`), each rule once, in
  declaration order. The components whose nominative already contains the bare form (*sataa*, *tuhatta*,
  *miljoonaa*) are first brought back to it, so no component is inflected twice; no unit word is a substring of
  another component. Partitive: *kahtakymmentäyhtä*, *sataakahtakymmentäviittä*, *viittäsataakahtakymmentäkolmea*,
  *kolmeatuhatta*, *kahtakymmentäyhtätuhatta sataakahtakymmentäyhtä*. Genitive: *kahdenkymmenenyhden*,
  *sadankahdenkymmenenviiden*, *kolmentuhannen*, *kahdenkymmenenyhdentuhannen sadankahdenkymmenenyhden*. The forms of
  the rows not quoted by #5 apply the same rule to the quoted component forms.

**Corrected legacy outputs.** *kaksikymmentä yksi*, *sata yksi*, *kaksi tuhat* (nominative); partitive *kaksikymmentä
yhtä*, *kaksisataa yhtä* (spaces, and the multiplier left in the nominative: #5 gives *kahtakymmentäyhtä*); genitive
*kahdenkymmenen yhden*, *kahdensadan kahdenkymmenen yhden* (spaces). The former comment claiming that the multiplied
tens and hundreds were "already in a partitive-compatible form" was wrong for the multiplier itself (*kaksi* →
*kahta*).

**Not opened.** The ordinals from 2000 (#6: no source) and the millions (out of the NTS-30E scope). The zero keeps
its nominative *nolla* in every case, as before; its inflection is not covered by the consulted pages.

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
inflects these names correctly but does not respell them: NTS-36, closed on 2026-10-20 (see "NTS-36 and NTS-42 F").

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
| FI | Ordinal marker in every part, one word (*viideskymmenesviides*); *kahdeskymmenesensimmäinen* more common than *…yhdes*; 11–19 marked on the first part; inflected compounds inflect every part (*kolmannenkymmenennenkuudennen*) | Kielitoimiston ohjepankki "Luvut ja numerot: järjestysluvut" and "… järjestyslukujen taivuttaminen"; Aalto/Kielenhuollon käsikirja paradigms (1, 3, 5, 17, 20, 100, 1000) | **Implemented** nominative 0–1999; other cases and ≥ 2000 fail closed (the cardinal is correct since NTS-30E, *kaksituhatta*, but the round-thousand ordinals are not sourced) |
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
| EU | Ordinal = cardinal in letters + *-garren* ("… ehungarren, milagarren …"), *batgarren* separate | Euskaltzaindia rule 18 (PDF) | **Widened** to 1–999 999 999 (≥ 10^9 NTS-30; the cardinal is composed since NTS-30F, the ordinals stay closed) |

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
| ES | Apocope of *uno* and its compounds before *mil* and the nouns *millón/millones* (*veintiún mil*, *treinta y un mil*, *ciento un mil*, *un millón*); *mil* not *un mil*; *mil millones*; *ciento uno* in two words | RAE/ASALE DPD *cardinales* (table, §2, §3, §4), *uno* §2.2–2.3, *mil* §1 | **Corrected**; feminine before *mil* keeps the apocope (allowed by §3); *mil millones* + millions not composed (NTS-30; composed since NTS-30F) |

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
- NTS-39 (2026-10-16):
  - IJP (ÚJČ AV ČR), "Počítaný předmět po číslovkách": <https://prirucka.ujc.cas.cz/?id=792>; "Členění čísel …": <https://prirucka.ujc.cas.cz/?id=791>; entries *lomit* <https://prirucka.ujc.cas.cz/?slovo=lomeno>, *třináctina* <https://prirucka.ujc.cas.cz/?slovo=t%C5%99in%C3%A1ctina>, *stotisícina* <https://prirucka.ujc.cas.cz/?slovo=stotis%C3%ADcina>, *miliardtina* <https://prirucka.ujc.cas.cz/?slovo=miliardtina>, *dva* <https://prirucka.ujc.cas.cz/?slovo=dva>, *sto* <https://prirucka.ujc.cas.cz/?slovo=sto>
  - Morfológia slovenského jazyka (1966), pp. 300–399: <https://www.juls.savba.sk/ediela/msj/pdf100/MSJ_300_399_LQ.pdf>
  - A. Jarošová, Slovenská reč 86 (2021): <https://www.sav.sk/journals/uploads/12101101zlozene-cislovky-so-zretelom-na-pisanie-spolu-a-oddelene.pdf>
  - J. Páleníková (JÚĽŠ SAV), Quark: <https://www.quark.sk/cislovky-dvadsatjeden-tridsatjeden-styridsatjeden/>
  - KSSJ 4 via slovnik.aktuality.sk, e.g. *pätina*: <https://slovnik.aktuality.sk/pravopis/kratky-slovnik/?q=p%C3%A4tina>, *dva*: <https://slovnik.aktuality.sk/pravopis/kratky-slovnik/?q=dva>
  - P. Ondrus, Slovenská reč 33 (1968) (the NTS-30H scan)
  - І. Літвінова, Українська мова 6 кл., ч. 6: <https://ua.izzi.digital/DOS/360496/datastore/21/publication/360496/files/2022/12/29/854a8ad143d6c75b7060c817b05b5302_ukrains_ka_mova._navcal_nij_posibnik_dla_6_klasu_zzso__avt._i._m._litvinova__c._6._cislivnik._zajmennik._povtorenna.pdf>; "Числівник" (НККЕП): <http://nkkep.com/wp-content/uploads/2020/02/CHy-slivny-k-PRAVY-LA.pdf>
  - Г. Митринов, ИБЕ-БАН: <https://ibl.bas.bg/ezikovi_spravki/za-edna-osobenost-v-upotrebata-na-tchislitelnite-imena/>; Клет България, Математика 5. клас, урок 58: <https://bg.izzi.digital/DOS/3887/9974.html>
- NTS-40 and NTS-41 (2026-10-16):
  - Український правопис 2019 (slovnyk.ua): §105 <https://slovnyk.ua/pravopys.php?prav_par=105>, §106 <https://slovnyk.ua/pravopys.php?prav_par=106>, §107 <https://slovnyk.ua/pravopys.php?prav_par=107>; 2015 edition §70 (the *п'ятсот* table): <https://pravopys.net/sections/70/>
  - Горох (VESUM), словозміна: *тисяча* <https://goroh.pp.ua/%D0%A1%D0%BB%D0%BE%D0%B2%D0%BE%D0%B7%D0%BC%D1%96%D0%BD%D0%B0/%D1%82%D0%B8%D1%81%D1%8F%D1%87%D0%B0>, *мільйон* <https://goroh.pp.ua/%D0%A1%D0%BB%D0%BE%D0%B2%D0%BE%D0%B7%D0%BC%D1%96%D0%BD%D0%B0/%D0%BC%D1%96%D0%BB%D1%8C%D0%B9%D0%BE%D0%BD>, *третій* <https://goroh.pp.ua/%D0%A1%D0%BB%D0%BE%D0%B2%D0%BE%D0%B7%D0%BC%D1%96%D0%BD%D0%B0/%D1%82%D1%80%D0%B5%D1%82%D1%96%D0%B9>
  - E. Prandstetter, "Čtení matematických značek a výrazů", Naše řeč 22 (1938), 3, 78–86: <http://nase-rec.ujc.cas.cz/archiv.php?art=3262>
  - IJP entries *tři* (cached with *třetina*), *čtyři* <https://prirucka.ujc.cas.cz/?slovo=%C4%8Dty%C5%99i>, *tisíc* <https://prirucka.ujc.cas.cz/?slovo=tis%C3%ADc>, *milion* <https://prirucka.ujc.cas.cz/?slovo=milion>, *miliarda* <https://prirucka.ujc.cas.cz/?slovo=miliarda>, *polovina* <https://prirucka.ujc.cas.cz/?slovo=polovina>; cs.wiktionary *jeden* <https://cs.wiktionary.org/wiki/jeden>, *pět* <https://cs.wiktionary.org/wiki/p%C4%9Bt>
  - Morfológia slovenského jazyka (1966), pp. 100–199 (patterns *žena*, *ulica*): <https://www.juls.savba.sk/ediela/msj/pdf100/MSJ_100_199_LQ.pdf>; pp. 300–399 (numerals): <https://www.juls.savba.sk/ediela/msj/pdf100/MSJ_300_399_LQ.pdf>
