@NumberToString @EE
Feature: Ewe number conversion

# NTS-20 rebuilt the Ewe cardinals and restored the ordinals. Sources and orthography
# (Utils.NumberToString/docs/NTS-08-linguistic-sources.md, section NTS-20):
#   [B] Biblica Open Ewe Contemporary Scriptures (Ghana, 1988/2006/2020, eBible.org, CC BY-SA):
#       numbers written out with the digits in parentheses, e.g. GEN 5:27 "alafa asiekɛ blaade-vɔ-asiekɛ (969)".
#   [K] S. W. Dzablu-Kumah, Basic Ewe for Foreign Students, 2nd ed., Univ. of Cologne (lesson III.2).
#   [I] Ewe Basic Course, Indiana University 1968 (ERIC ED028444), tonal transcription (ordinal rule, gbãtɔ).
#   [P] Peace Corps Togo, Ewe O.P.L. workbook, 2010 (21, 101, 122, glossary "zero").
#   [W] Wiktionary Ewe numerals (Westermann 1905, Dzablu-Kumah 2015, Nuseline's dictionary 2017) / Omniglot.
# Orthography: standard Ewe letters without tone marks; nasal tilde kept (etɔ̃, atɔ̃); "one" is ɖekɛ after
# wui- and vɔ ([K], [B], [W]); seven "adre" ([B] 591 occurrences, [W]); nine "asieke" ([K], [I], [W], [P]);
# the tens-units connector "vɔ" is written as a separate word ([K], [W]; [B] also hyphenates it).
# Evidence column: A = explicitly attested value, R = productive application of a sourced rule.
# P = project-defined productive Conway-Wechsler scale name (NTS-23), not an individually attested Ewe form.

Scenario Outline: Units, teens and tens
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected             | evidence      |
    | 1      | ɖeka                 | A [K][I][W]   |
    | 2      | eve                  | A [K][I][W]   |
    | 3      | etɔ̃                  | A [K][I][W]   |
    | 4      | ene                  | A [K][I][W]   |
    | 5      | atɔ̃                  | A [K][I][W]   |
    | 6      | ade                  | A [K][I][W]   |
    | 7      | adre                 | A [B][W]      |
    | 8      | enyi                 | A [K][I][W]   |
    | 9      | asieke               | A [K][I][W]   |
    | 10     | ewo                  | A [K][I][W]   |
    | 11     | wuiɖekɛ              | A [K][B][W]   |
    | 12     | wuieve               | A [K][B][W]   |
    | 15     | wuiatɔ̃               | A [K][W]      |
    | 19     | wuiasieke            | A [K][W]      |
    | 20     | blaeve               | A [K][B][W]   |
    | 21     | blaeve vɔ ɖekɛ       | A [K][W]      |
    | 22     | blaeve vɔ eve        | A [K][P]      |
    | 29     | blaeve vɔ asieke     | A [K]         |
    | 30     | blaetɔ̃               | A [K][B][W]   |
    | 31     | blaetɔ̃ vɔ ɖekɛ       | R [K]         |
    | 40     | blaene               | A [K][W]      |
    | 50     | blaatɔ̃               | A [K][W]      |
    | 60     | blaade               | A [K][W]      |
    | 70     | blaadre              | A [B][W]      |
    | 80     | blaenyi              | A [K][W]      |
    | 90     | blaasieke            | A [K][W]      |
    | 99     | blaasieke vɔ asieke  | R [K]         |

# Hundreds: the noun alafa precedes its multiplier ([K] 100-400, [B]). "kple" is written before a
# remainder of 1-10 in every source ([B] units 8/8 and ten 4/4, [P] 101, [W] 101); before 11-99 it is
# optional ([B] writes both, e.g. 120 and 153 without it, 122 with it; [P] 122 without it) and the
# configuration produces the form without it, the only one found in the attested ordinals (150th, 480th).
Scenario Outline: Hundreds
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected                              | evidence              |
    | 100    | alafa ɖeka                            | A [K][I][B][P]        |
    | 101    | alafa ɖeka kple ɖeka                  | A [P][W]              |
    | 105    | alafa ɖeka kple atɔ̃                   | A [B] GEN 5:6         |
    | 109    | alafa ɖeka kple asieke                | R [B]                 |
    | 110    | alafa ɖeka kple ewo                   | A [B] EZR 8:12        |
    | 111    | alafa ɖeka wuiɖekɛ                    | R [B] 1CH 15:10 (112) |
    | 119    | alafa ɖeka wuiasieke                  | R ([B] GEN 11:25 has the optional kple) |
    | 120    | alafa ɖeka blaeve                     | A [B] 1CH 15:5        |
    | 122    | alafa ɖeka blaeve vɔ eve              | A [P]                 |
    | 153    | alafa ɖeka blaatɔ̃ vɔ etɔ̃              | A [B] JOH 21:11       |
    | 199    | alafa ɖeka blaasieke vɔ asieke        | R                     |
    | 200    | alafa eve                             | A [K][B]              |
    | 201    | alafa eve kple ɖeka                   | R [B]                 |
    | 205    | alafa eve kple atɔ̃                    | A [B] GEN 11:32       |
    | 210    | alafa eve kple ewo                    | R [B]                 |
    | 234    | alafa eve blaetɔ̃ vɔ ene               | R [B] 1KI 20:15 (232) |
    | 276    | alafa eve blaadre vɔ ade              | A [B] ACT 27:37       |
    | 500    | alafa atɔ̃                             | A [B] GEN 5:32        |
    | 999    | alafa asieke blaasieke vɔ asieke      | R                     |

# Thousands: the noun akpe precedes its multiplier ([K] 1000/2000, [B] passim), and the multiplier is
# the standalone cardinal ([B] 144 000 "akpe alafa ɖeka blaene-vɔ-ene", 41 000 "akpe blaene-vɔ-ɖekɛ").
# "kple" joins the thousands to a lower part below 100 ([B] 8/8: 1005, 1017, 1052, 2056 ...) and never
# to a lower part with hundreds ([B] 138/140: 1200, 1254, 2172 ...).
Scenario Outline: Thousands
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected                                                          | evidence                |
    | 1000   | akpe ɖeka                                                         | A [K][B][P][W]          |
    | 1001   | akpe ɖeka kple ɖeka                                               | R [B]                   |
    | 1005   | akpe ɖeka kple atɔ̃                                                | A [B] 1KI 4:32          |
    | 1010   | akpe ɖeka kple ewo                                                | R [B]                   |
    | 1017   | akpe ɖeka kple wuiadre                                            | A [B] EZR 2:39          |
    | 1052   | akpe ɖeka kple blaatɔ̃ vɔ eve                                      | A [B] EZR 2:37          |
    | 1100   | akpe ɖeka alafa ɖeka                                              | R [B]                   |
    | 1101   | akpe ɖeka alafa ɖeka kple ɖeka                                    | R [B]                   |
    | 1200   | akpe ɖeka alafa eve                                               | A [B] 2CH 12:3          |
    | 1220   | akpe ɖeka alafa eve blaeve                                        | R [B]                   |
    | 1254   | akpe ɖeka alafa eve blaatɔ̃ vɔ ene                                 | A [B] EZR 2:7           |
    | 1260   | akpe ɖeka alafa eve blaade                                        | A [B] REV 11:3          |
    | 2000   | akpe eve                                                          | A [K][B]                |
    | 2001   | akpe eve kple ɖeka                                                | R [B]                   |
    | 2345   | akpe eve alafa etɔ̃ blaene vɔ atɔ̃                                  | R [B]                   |
    | 3000   | akpe etɔ̃                                                          | A [B] 1KI 4:32          |
    | 10000  | akpe ewo                                                          | A [B][W]                |
    | 12000  | akpe wuieve                                                       | A [B] NUM 31:5          |
    | 20000  | akpe blaeve                                                       | A [B] 2SA 18:7          |
    | 21000  | akpe blaeve vɔ ɖekɛ                                               | R [B] NUM 1:41 (41 000) |
    | 22000  | akpe blaeve vɔ eve                                                | A [B] NUM 3:39          |
    | 100000 | akpe alafa ɖeka                                                   | A [B] 1KI 20:29         |
    | 120000 | akpe alafa ɖeka blaeve                                            | A [B] 1CH 12:37         |
    | 144000 | akpe alafa ɖeka blaene vɔ ene                                     | A [B] REV 7:4           |
    | 601000 | akpe alafa ade kple ɖeka                                          | A [B] NUM 26:51         |
    | 999999 | akpe alafa asieke blaasieke vɔ asieke alafa asieke blaasieke vɔ asieke | R                  |

# Zero: every consulted source gives the same answer in its "zero" row ([P] glossary "zero: naneke o,
# gbɔlo"; Omniglot "nanekeo"; Wikivoyage "nadɛkɛ o"). gbɔlo is the adjective "empty" ([B] asi gbɔlo
# "empty hands"; [P] glossary "empty: ƒuƒlu, gbɔlo") and is not produced. No source attests a numeral
# for zero distinct from "naneke o" (literally "nothing"); it is the standalone answer to "how many?".
Scenario: Zero
    Given I use the "EE" number converter
    When I convert the cardinal number 0
    Then the result is "naneke o"

# NTS-22. Millions: the noun miliɔn precedes its multiplier, the standalone cardinal, like akpe
# ([B] 1CH 21:5 "miliɔn ɖeka akpe alafa ɖeka (1,100,000)", REV 9:16 "miliɔn alafa eve (200,000,000)";
# [W] "miliɔn ɖeka"; CLDR compact "miliɔn 0"). [P]'s "akpe akpe" is accepted, not produced: the Bible
# only uses "akpe akpewo" for an indefinite "thousands upon thousands" (DAN 7:10, GEN 24:60).
# The groups are joined like the thousands ("kple" before a lower group below 100), a productive
# extension: no source writes a million followed by a lower group below 100 000.
Scenario Outline: Millions
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number    | expected                                                    | evidence           |
    | 1000000   | miliɔn ɖeka                                                 | A [W] Omniglot     |
    | 1000001   | miliɔn ɖeka kple ɖeka                                       | R                  |
    | 1001000   | miliɔn ɖeka kple akpe ɖeka                                  | R                  |
    | 1001001   | miliɔn ɖeka kple akpe ɖeka kple ɖeka                        | R                  |
    | 1100000   | miliɔn ɖeka akpe alafa ɖeka                                 | A [B] 1CH 21:5     |
    | 1100001   | miliɔn ɖeka akpe alafa ɖeka kple ɖeka                       | R                  |
    | 2000000   | miliɔn eve                                                  | R                  |
    | 3000000   | miliɔn etɔ̃                                                  | R                  |
    | 10000000  | miliɔn ewo                                                  | R                  |
    | 20000000  | miliɔn blaeve                                               | R                  |
    | 21000000  | miliɔn blaeve vɔ ɖekɛ                                       | R                  |
    | 100000000 | miliɔn alafa ɖeka                                           | R                  |
    | 122000000 | miliɔn alafa ɖeka blaeve vɔ eve                             | R                  |
    | 200000000 | miliɔn alafa eve                                            | A [B] REV 9:16     |
    | 999000000 | miliɔn alafa asieke blaasieke vɔ asieke                     | R                  |
    | 999999999 | miliɔn alafa asieke blaasieke vɔ asieke akpe alafa asieke blaasieke vɔ asieke alafa asieke blaasieke vɔ asieke | R |

# NTS-23. Larger scales follow the Conway-Wechsler short scale (SCALE-SHORT tables, prefix + "li" + "ɔn").
# Sourced linguistic anchors (ticket premises, CLDR compact patterns): miliɔn 10^6, biliɔn 10^9, triliɔn 10^12.
# quadriliɔn and above are a project-defined productive Conway-Wechsler continuation (P), not individually
# attested Ewe forms. CLDR RBNF's divergent system (10^9 "miliɔn akpe", biliɔn 10^12) and its "kpakple"
# connector are not used: "kpakple" is only attested as a nominal "and" ([B], 99 occurrences, none numeric).
Scenario Outline: Billions and higher scales
    Given I use the "EE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number                 | expected                                    | evidence |
    | 1000000000             | biliɔn ɖeka                                 | A anchor |
    | 1000000000000          | triliɔn ɖeka                                | A anchor |
    | 2000000000             | biliɔn eve                                  | R        |
    | 21000000000            | biliɔn blaeve vɔ ɖekɛ                       | R        |
    | 200000000000000        | triliɔn alafa eve                           | R        |
    | 1001000000             | biliɔn ɖeka kple miliɔn ɖeka                | R        |
    | 1000001000             | biliɔn ɖeka kple akpe ɖeka                  | R        |
    | 1000000001             | biliɔn ɖeka kple ɖeka                       | R        |
    | 1000000000001          | triliɔn ɖeka kple ɖeka                      | R        |
    | 1000000000000000       | quadriliɔn ɖeka                             | P        |
    | 999000000000000000     | quadriliɔn alafa asieke blaasieke vɔ asieke | P        |
    | 1000000000000000000    | quintiliɔn ɖeka                             | P        |
    | 1000000000000000000000 | sextiliɔn ɖeka                              | P        |

# Ordinals: every ordinal except "first" (gbãtɔ) adds -lia to the cardinal ([I]: "The ordinal numerals,
# with the exception of /gbãto/ 'first', are formed by adding /-lia/ to each of the numbers"). [B] attests
# the suffix on the last element of compounds: wuiɖekɛlia, blaevelia, blaeve-vɔ-ɖekɛlia,
# blaeve-vɔ-etɔ̃lia, ŋkeke alafa ɖeka blaatɔ̃lia (150th, GEN 8:3), ƒe alafa ene blaenyilia (480th, 1KI 6:1).
Scenario Outline: Ordinal numbers
    Given I use the "EE" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected                                  | evidence               |
    | 1      | gbãtɔ                                     | A [I][B][W]            |
    | 2      | evelia                                    | A [I][B][W]            |
    | 3      | etɔ̃lia                                    | A [I][B][W]            |
    | 4      | enelia                                    | A [B][W]               |
    | 5      | atɔ̃lia                                    | A [B][W]               |
    | 6      | adelia                                    | A [B][W]               |
    | 7      | adrelia                                   | A [B] GEN 2:2, [W]     |
    | 8      | enyilia                                   | A [B][W]               |
    | 9      | asiekelia                                 | A [B][W]               |
    | 10     | ewolia                                    | A [B][W]               |
    | 11     | wuiɖekɛlia                                | A [B]                  |
    | 12     | wuievelia                                 | A [B]                  |
    | 19     | wuiasiekelia                              | A [B] 1CH 24:16        |
    | 20     | blaevelia                                 | A [B] NUM 10:11        |
    | 21     | blaeve vɔ ɖekɛlia                         | A [B] EXO 12:18        |
    | 22     | blaeve vɔ evelia                          | A [B] 1CH 24:17        |
    | 29     | blaeve vɔ asiekelia                       | R [I]                  |
    | 30     | blaetɔ̃lia                                 | R [I]                  |
    | 31     | blaetɔ̃ vɔ ɖekɛlia                         | R [I]                  |
    | 99     | blaasieke vɔ asiekelia                    | R [I]                  |
    | 100    | alafa ɖekalia                             | A [B] (hundredth part) |
    | 101    | alafa ɖeka kple ɖekalia                   | R [I]                  |
    | 122    | alafa ɖeka blaeve vɔ evelia               | R [I]                  |
    | 150    | alafa ɖeka blaatɔ̃lia                      | A [B] GEN 8:3          |
    | 200    | alafa evelia                              | R [I]                  |
    | 234    | alafa eve blaetɔ̃ vɔ enelia                | R [I]                  |
    | 480    | alafa ene blaenyilia                      | A [B] 1KI 6:1          |
    | 999    | alafa asieke blaasieke vɔ asiekelia       | R [I]                  |
    | 1000   | akpe ɖekalia                              | R [I]                  |
    | 1001   | akpe ɖeka kple ɖekalia                    | R [I]                  |
    | 2000   | akpe evelia                               | R [I]                  |
    | 10000  | akpe ewolia                               | R [I]                  |
    | 2345   | akpe eve alafa etɔ̃ blaene vɔ atɔ̃lia       | R [I]                  |
    | 1000000   | miliɔn ɖekalia                          | A [W]                  |
    | 2000000   | miliɔn evelia                           | R [I]                  |
    | 21000000  | miliɔn blaeve vɔ ɖekɛlia                | R [I]                  |
    | 200000000 | miliɔn alafa evelia                     | R [I]                  |
    | 1100000   | miliɔn ɖeka akpe alafa ɖekalia          | R [I]                  |
    | 1100001   | miliɔn ɖeka akpe alafa ɖeka kple ɖekalia | R [I]                 |
    | 1000000000       | biliɔn ɖekalia                   | R [I]                  |
    | 1000000000000    | triliɔn ɖekalia                  | R [I]                  |
    | 1000000000000000 | quadriliɔn ɖekalia               | R [I] P                |

# No source attests an ordinal of zero: the suffix is never applied to "naneke o".
Scenario: Ordinal zero is rejected
    Given I use the "EE" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available

# Decimal and fraction wording only change lexically with the rebuilt cardinals: the "kpɔ" decimal
# separator and the "kple" fraction connector are not validated by NTS-20.
Scenario Outline: Decimal numbers
    Given I use the "EE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected               |
    | 1.5    | ɖeka kpɔ atɔ̃           |
    | 12.34  | wuieve kpɔ etɔ̃ ene     |

Scenario: Fraction connector wording
    Given I use the "EE" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "etɔ̃ kple eve"

# No reliable Ewe source for minutes was found (only "ga eto" / "ga eto kple afa"); a clock that
# only knows :00 and :30 would round aggressively, so clock-time stays deliberately unsupported.
Scenario: Idiomatic clock-time conversion is unsupported
    Given I use the "EE" number converter
    Then the converter does not support clock-time conversion
