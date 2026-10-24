# NTS-34 — decimal reading audit of every built-in language (2026-10-24)

NTS-30A built the decimal reading modes (`DecimalReadingMode.LanguageDefault`, `DigitByDigit`, `MathematicalFraction`)
and the declarative `decimalDigitByDigitMaxDigits`, `mixedFractionSeparator`, `<DecimalDigits>` and `<Fractions>`; it
audited FR and EN only. Every other language read its decimals digit by digit under `LanguageDefault`, with no limit,
on no source. This audit classifies each root language; a regional child (`baseOn`) inherits the decision of its root
unless a source shows a regional difference (none was found).

## Categories

| Category | Meaning | Configuration |
|---|---|---|
| A | The digit-by-digit reading is attested as a normal reading | unchanged: `LanguageDefault` reads the digits, no limit |
| B | Named decimal fractions, representable | `<Fractions>`, `mixedFractionSeparator` (FR, EN: NTS-30A, unchanged) |
| C | The normative reading is known but the primitives cannot render it exactly | `decimalDigitByDigitMaxDigits="0"` |
| D | No authoritative source establishes the automatic reading | `decimalDigitByDigitMaxDigits="0"` |

With `0`, `LanguageDefault` throws `NotSupportedException` for every non-empty decimal part (a padded zero one
included, `Convert(21m, 2)`); an integer value, a caller `DecimalSuffix` and `OmitZeroDecimals` behave as before.
`DigitByDigit` is a reading the caller requests: it is unchanged in every language (integer, configured separator word,
each digit), and is not a claim that it is the natural reading. `MathematicalFraction` needs a configured fraction: it
throws in every C/D language, as before.

No numeric limit (`N` > 0) was configured: no source states one, and a limit chosen "by prudence" would be arbitrary.
No new primitive was added: the C readings would need declined fraction nouns selected by the numerator (CS, SK, PL,
RU, UK, BG, HR), a decimal part read as one cardinal with its leading zeros (SV, VN, IT, HE), a denominator placed
before its numerator (TR), or separate words and attributive forms in a language whose word separator is empty (HU):
each is a new subsystem, and the backlog closes fail-closed rather than opening one.

## Source hierarchy

Academy or normative body, academic grammar, institutional dictionary, ministry of education or official textbook,
university, recognised dictionary, specialised secondary source. Blogs, forums, Wiktionary, commercial pages and
machine output were used only to find a lead; where nothing better was found the language is D, and a reading
supported only by school sites of unknown standing is marked as such.

Read in full (page or PDF fetched): DE, DA, NO, ES, ZH, HU, CS, PL, EL, SV, KO, FI, and the NTS-30A FR/EN sources.
Known from search-engine excerpts only (the page refused the download or was not fetched): SK (advisory answers), RU,
UK, BG, HR, VN, IT, PT, CA, RO, HE, TR, AR, FA, EU. These excerpts can only close a language, never open a reading,
so the uncertainty they carry cannot produce an unsourced output.

## Matrix

| Language (children) | Source(s) consulted | Observed reading | Engine capability | Decision | Configuration | Tests |
|---|---|---|---|---|---|---|
| DE (de-CH, de-LI) | PH Niederösterreich, G. Egger, *Dezimalzahlen* (2022): "5,01 „fünf Komma null eins“" (already NTS-30A) | digit by digit, zero *null*, 1 *eins* | `<DecimalDigit digit="1" string="eins">` (NTS-30A) | **A** | unchanged | `CategoryA_LanguageDefault_IsDigitByDigit` |
| DA (DA-DK) | Lex (Den Store Danske), *decimal*: "På dansk er det kutyme at udtale decimaler separat. Således udtales fx 0,88 ikke "nul komma otteogfirs", men "nul komma otte otte"" (amounts of money excepted) | digit by digit, zero *nul* | yes | **A** | unchanged | idem |
| NO (NB, NB-NO) | Nasjonalt senter for matematikk i opplæringen (NTNU), *Desimaltall*: "I skolen bør vi lese siffer etter komma som enkeltsiffer, slik: 0,123 leses "null komma en-to-tre"" (it notes that everyday usage reads *null komma femtifem*) | digit by digit prescribed, zero *null* | yes (the hyphens of *en-to-tre* transcribe speech; the engine spaces the digits) | **A** | unchanged | idem |
| ES | RAE/ASALE, *Diccionario panhispánico de dudas*, 2nd ed., "números" §3.4: "En la lengua oral, es también frecuente leer simplemente la secuencia de signos de que se componen, incluyendo el separador: 7,08 = siete coma cero ocho […] plenamente admisible en el registro oral" | digit by digit admitted orally; written *veinte (unidades) con/y cincuenta y ocho (centésimas)* | the oral reading yes; the fraction reading would replace it under `LanguageDefault` (fractions take precedence), so it is not configured | **A** | unchanged | idem |
| ZH | GB/T 15835-2011 *出版物上数字用法* §5.2.5: "“95.06”的汉字数字形式为“九十五点零六”（不写为“九十五点〇六”）"; example "0.5（零点五）" | digit by digit, 点, 零 | yes | **A** | unchanged | idem |
| FR (FR-be, FR-ch, FR-ca, FR-fr) | NTS-30A: Éduscol, OQLF, Académie française | named fractions 1–11 digits, *et* | yes | **B** | unchanged | `CategoryB_Unchanged` |
| EN (EN-GB, EN-uk, EN-us) | NTS-30A: OpenStax *Prealgebra 2e* §5.1 | named fractions 1–5 digits, *and* | yes | **B** | unchanged | idem |
| HU (HU-HU) | MTA, *A magyar helyesírás szabályai*, 12th ed., rule 274: "38,6 (= harmincnyolc egész hat tized); 1,23 (= egy egész huszonhárom század)"; rule 293: "egy negyed, két harmad, harmincnyolc egész egy tized" (written apart) | integer + *egész* + numerator + *tized*/*század*, separate words, attributive *két* | no: HU's word separator is empty (*egyegészkettő*), and a numerator or integer 2 would be the standalone *kettő* | **C** | `decimalDigitByDigitMaxDigits="0"` | `CategoriesCD_*` |
| CS (CS-CZ) | ÚJČ AV ČR, *Internetová jazyková příručka*, "Čtení desetinných čísel": "1,2 – jedna celá dvě (desetiny)", "5,1 – pět celých jedna (desetina)", "1000,05 – tisíc celých pět setin", "1024,007 – tisíc dvacet čtyři celé/celých sedm tisícin" | *celá/celé/celých* + declined fraction noun | no (selection of *celá* by the integer, of the noun by the numerator) | **C** | idem | idem |
| SK (SK-SK) | TASR series prepared "v spolupráci s Jazykovedným ústavom Ľudovíta Štúra SAV", *Ako sa správne čítajú desatinné čísla* (teraz.sk): "Celé číslo skrátene nazývame celá", the decimal part in "desatiny, stotiny a občas aj tisíciny"; the language-advisory answers (jazykovaporadna.sme.sk q/704, q/1680: *nula celých, jedna celá, dve celé, päť celých*, "2,3 = dve celé (a) tri desatiny") were known from search excerpts only, the pages refusing the download | *celá/celé/celých* + declined fraction noun | no | **C** | idem | idem |
| PL | Zintegrowana Platforma Edukacyjna (zpe.gov.pl, Politechnika Łódzka), "Zapisywanie i odczytywanie ułamków dziesiętnych": "trzy i sześć dziesiątych", "pięć i czternaście tysięcznych", "siedemnaście setnych" | integer + *i* + declined fraction | no (*dziesiąta/dziesiąte/dziesiątych* by the numerator) | **C** | idem | idem |
| RU | School material only (repetitor.1c.ru, yaklass.ru; Грамота.ру not found): "2,3 — два целых три десятых", "5,08 — пять целых восемь сотых" | *целая/целых* + declined fraction | no | **C** (school sources) | idem | idem |
| UK (UK-UA) | School material (miyklas.com.ua, NUS 5th grade): "9,1 – дев'ять цілих одна десята", "0,25 – нуль цілих двадцять п'ять сотих" | *ціла/цілих* + declined fraction | no | **C** (school sources) | idem | idem |
| BG (BG-BG) | Textbook platforms (izzi.digital, domashno.bg, 5th grade): "3,14 → три цяло и четиринадесет стотни", "една стотна (или нула цяло и една стотна)"; ИБЕ-БАН not found | integer + *цяло и* + fraction agreeing with the numerator | no (the integer agrees with the neuter *цяло*, the noun with the numerator); the configured separator word *цяло* is itself an agreeing form, kept for the explicit `DigitByDigit` only | **C** (textbook sources) | idem | idem |
| HR (HR-HR) | School material (edutec.hr): "4,32 – četiri cijela i trideset dvije stotinke"; Institut za hrvatski jezik: notation only (comma) | *cijeli/cijela/cijelih* + declined fraction | no | **C** (school sources) | idem | idem |
| EL | Ministry of Education textbook (ebooks.edu.gr, Μαθηματικά ΣΤ΄ Δημοτικού): "εκατόν δύο και σαράντα πέντε χιλιοστά"; "Το δεκαδικό μέρος διαβάζεται με το όνομα της αξίας του τελευταίου ψηφίου" | integer + *και* + named fraction | not under `LanguageDefault`: configured fractions would read *εκατόν δύο κόμμα σαράντα πέντε χιλιοστά*, attested by a teacher blog only | **C** | idem | idem |
| SV (SV-SE) | Isof, Språkrådet, Frågelådan 21402: "Vi skriver alltså 10,85 och läser ut det tio komma åttiofem" | the decimal part read as one number | no (no mode reads the decimal part as a cardinal) | **C** | idem | idem |
| VN (VI, VI-VN) | SGK Toán 5 as reproduced by school sites (luyenthi123, vietjack): "286,703 đọc là: hai trăm tám mươi sáu phẩy bảy trăm linh ba", "101,003 … phẩy không trăm linh ba" | the decimal part read as one number | no | **C** (school sources) | idem | idem |
| IT | School material (youmath, scuola): "12,724 si legge dodici e settecentoventiquattro millesimi", "48,51 … quarantotto virgola cinquantuno"; Accademia della Crusca not found | named fraction with *e*, or the decimal part as a number | no | **C** (school sources) | idem | idem |
| PT | School material (Brazil): "2,04 é dois inteiros e quatro centésimos", "cinco vírgula cinquenta e três"; Ciberdúvidas not found | *inteiro(s) e* + fraction, or the decimal part as a number | no | **C** (school sources) | idem | idem |
| CA (ca-ES, ca-ES-valencia) | Generalitat Valenciana, portal.edu.gva.es, "Lectura i escriptura de nombres decimals"; school material: "8,45 … vuit unitats i quaranta-cinc centèsimes" or "vuit coma quaranta-cinc"; IEC/Optimot not found | *unitat(s) i* + fraction, or the decimal part as a number | no | **C** | idem | idem |
| RO (RO-RO) | Romanian 5th-grade textbook (Capitolul 5, *Numere zecimale*): "8,027 … opt întregi și douăzeci și șapte miimi" or "opt virgulă zero douăzeci și șapte"; DOOM 3 / GALR: no statement | *întregi și* + fraction, or zero + the rest as a number | no | **C** | idem | idem |
| HE | Academy of the Hebrew Language, "על הבעת המספר המעורב": common usage "שלושה אחוזים נקודה עשרים ואחת"; the question was returned to the grammar committee, undecided | the decimal part read as a number | no | **C** | idem | idem |
| TR (TR-TR) | School material (TDK not found): "3,25 üç tam yüzde yirmi beş", "0,5 sıfır tam onda beş"; everyday "üç virgül yirmi beş" | *tam* + denominator + numerator, or the decimal part as a number | no (denominator before the numerator) | **C** (school sources) | idem | idem |
| AR | School material: "4,36 يُقرأ أربعة فاصلة ستة وثلاثون من مئة" | the decimal part read as a number with its denominator | no | **C** (school sources) | idem | idem |
| FA (FA-IR) | School material (gama.ir, 5th grade): "2/945 (دو و نهصد و چهل و پنج هزارم)" | integer + *و* + named fraction | no | **C** (school sources) | idem | idem |
| EU (eu-ES) | Euskaltzaindia, Jagonet: notation only; school material: fractions in *hamarren*, *ehunen*, *milaren* | named fraction | no | **C** (school sources) | idem | idem |
| JA | MEXT 学習指導要領: no reading; school sites only (*れいてんいち*) | — | — | **D** | idem | idem |
| KO | NIKL, 온라인가나다 (2026-03-18): "아라비아 숫자로 표기한 소수를 한글로 표기하는 방법에 대해서는 <어문 규범>에 규정된 바 없습니다" | not regulated | — | **D** | idem | idem |
| NL | Taaladvies.net / Taalunie: notation only (comma); no ANS statement found; primary-school sites only | — | — | **D** | idem | idem |
| FI | Kielitoimiston ohjepankki, "Luvut ja numerot: desimaaliluvut": one inflected one-digit example (*kahdeksaantoista pilkku neljään*), no rule for several digits | — | — | **D** | idem | idem |
| ID (ID-ID; MS, MS-MY inherit) | Badan Bahasa / KBBI / EYD: nothing found; media only | — | — | **D** | idem (MS inherits through `baseOn`) | idem |
| HI | NCERT, Central Hindi Directorate: nothing found | — | — | **D** | idem | idem |
| GL (gl-ES) | RAG: nothing found | — | — | **D** | idem | idem |
| SW (SW-KE, SW-TZ) | TIE *Hisabati* textbooks: only reachable as image flipbooks, not readable; nothing else found | — | — | **D** | idem | idem |
| EE | Westermann and SIL resources: no decimal reading; neither *kpɔ* nor *naneke o* as a decimal digit is attested | — | — | **D** | idem | idem |
| WO | Decree 2005-992, Wolof Ajami Reader, Gaye, Guérin (the NTS-19/21/26 sources): no decimal reading; *pojint* unattested | — | — | **D** | idem | idem |
| ZU | DBE isiZulu Grade 4–5 mathematics terminology: terms only (*amadesimali*); PanSALB: nothing | — | — | **D** | idem | idem |

## Corrected and closed outputs

No language was corrected to another reading: the A languages keep their output, and every C/D language moves from an
unsourced automatic reading to a documented `NotSupportedException` (dump category "deliberately fail-closed").

| Language | Before (`LanguageDefault`, 21.05) | After | `DigitByDigit` (unchanged) |
|---|---|---|---|
| HU | `huszonegyegésznullaöt` | `NotSupportedException` | `huszonegyegésznullaöt` |
| CS | `dvacet jedna celá nula pět` | idem | idem |
| NL | `eenentwintig komma nul vijf` | idem | idem |
| JA | `二十一点零五` | idem | idem |
| (every other C/D language) | its digit-by-digit reading | idem | idem |

The explicit `DigitByDigit` reading of 21.05 of every closed converter is pinned byte-identical to master `7bd9354b`
(`NumberToStringNts34Tests.s_closed`).

## Not done, deliberately

- No `FractionLexicalFormSelector`, decimal agreement rule or "decimal part as a cardinal" mode: each would be a new
  subsystem for a subset of the C languages; NTS-34 closes them fail-closed.
- ES fractions (*veinte con cincuenta y ocho centésimas*, DPD §3.4) were not configured: under `LanguageDefault` the
  fraction would replace the attested oral reading, and the written form needs *con*/*y* with optional *unidades*.
- The separator words of the closed languages (BG *цяло*, CS/SK *celá*, HU *egész*, WO *pojint*, EE *kpɔ*) are kept:
  they serve only the explicit `DigitByDigit`, whose contract is "integer, configured separator, digits".

## Links

- DE: PH Niederösterreich, G. Egger, *Dezimalzahlen* (2022), see `docs/NTS-08-linguistic-sources.md`, NTS-30A
- DA: <https://lex.dk/decimal>
- NO: <https://www.matematikksenteret.no/sites/default/files/attachments/page/Desimaltall1.pdf>
- ES: <https://www.rae.es/dpd/números>
- ZH: GB/T 15835-2011, <https://journal.ecjtu.edu.cn/uploadfile/hdjtdx/20241115/GBT%2015835%E2%80%942011%20%E5%87%BA%E7%89%88%E7%89%A9%E4%B8%8A%E6%95%B0%E5%AD%97%E7%94%A8%E6%B3%95.pdf>
- HU: <https://helyesiras.mta.hu/helyesiras/default/akh12> (rules 274, 293)
- CS: <https://prirucka.ujc.cas.cz/?id=791>
- SK: <https://www.teraz.sk/slovensko/slovencina-ako-sa-spravne-citaju-d/926094-clanok.html>; <https://jazykovaporadna.sme.sk/q/704/>, <https://jazykovaporadna.sme.sk/q/1680/> (excerpts)
- PL: <https://zpe.gov.pl/watek/LPoJeifmNk/5/a/2-zapisywanie-i-odczytywanie-ulamkow-dziesietnych/DuHIaZEM>
- RU: <https://repetitor.1c.ru/arithmetic/desyatichnye-drobi/>, <https://www.yaklass.ru/> (excerpts)
- UK: <https://www.miyklas.com.ua/> (5th grade NUS, "Запис і читання десяткового дробу"; excerpt)
- BG: <https://bg.izzi.digital/DOS/4033/16034.html> (excerpt)
- HR: <https://www.edutec.hr/citanje-i-zapisivanje-decimalnih-brojeva/>; <https://jezicni-savjetnik.hr/> (notation; excerpts)
- EL: <https://ebooks.edu.gr/ebooks/v/html/8547/2186/Mathimatika_ST-Dimotikou_html-empl/index1_2.html>
- SV: <https://frageladan.isof.se/faqs/21402>
- VN: <https://www.luyenthi123.com/toan-lop-5/hang-cua-so-thap-phan-doc-viet-so-thap-phan>, <https://vietjack.com/tai-lieu-tieu-hoc/chuong-2-so-thap-phan-toan-5.jsp> (excerpts)
- IT: <https://www.youmath.it/lezioni/algebra-elementare/lezioni-di-algebra-e-aritmetica-per-scuole-medie/967-cosa-sono-i-numeri-decimali.html> (excerpt)
- PT: <https://www.somatematica.com.br/fundam/decimais/decimais3.php>, <https://www.preparaenem.com/matematica/numeros-decimais.htm> (excerpts)
- CA: <https://portal.edu.gva.es/blogs/s1/wp-content/uploads/sites/130/2024/08/DLFE-2090129.pdf> (excerpt)
- RO: <https://medmateiasi.wordpress.com/wp-content/uploads/2015/09/5-a3-numere_rationale_pozitive_21.pdf>, <https://scoalavirtuala.ro/fractii-zecimale-explicate-pe-intelesul-tau/> (excerpts)
- HE: <https://hebrew-academy.org.il/על-הבעת-המספר-המעורב> (excerpt)
- TR: <https://www.dragoman.ist/tr/turkcede-sayilarin-yazilisi/> (excerpt)
- AR: <https://answers.mawdoo3.com/> ("ما أسهل طريقة لقراءة العدد العشري؟"; excerpt)
- FA: <https://gama.ir/tutorials/2341> (excerpt)
- EU: <https://www.euskaltzaindia.eus/index.php?option=com_jagonet&task=galdera&Itemid=423&lang=eu&id=20170065> (notation); <https://www.scribd.com/document/435417270/ZENBAKI-HAMARTARRAK-LEHEN-HEZKUNTZA> (excerpt)
- KO: <https://www.korean.go.kr/front/onlineQna/onlineQnaView.do?mn_id=90&qna_seq=328287>
- FI: <https://kielitoimistonohjepankki.fi/ohje/luvut-ja-numerot-desimaaliluvut/>
- NL: <https://taaladvies.net/komma-of-punt-bij-decimale-getallen/> (notation only)
- ZU: <https://www.education.gov.za/Portals/0/Documents/MTbBE/mttbe%20terminology/IsiZulu%20%20Grade%204%20and%205%20Mathematics%20Terminology.pdf> (terminology only)
- SW: TIE *Hisabati Darasa la Tano/Sita* (fliphtml5 flipbooks, not readable)
