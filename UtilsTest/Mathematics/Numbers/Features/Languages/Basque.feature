@NumberToString @EU
Feature: Basque number conversion

Scenario Outline: Cardinal numbers in the twenties and higher
    Given I use the "eu-ES" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | hogeita bat |
    | 31 | hogeita hamaika |
    | 38 | hogeita hemezortzi |
    | 57 | berrogeita hamazazpi |

Scenario Outline: Hundreds
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | ehun |
    | 205 | berrehun eta bost |

Scenario Outline: Decimal numbers
    Given I use the "EU-es" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | bat koma bost |

Scenario Outline: Thousands
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | mila |
    | 20000 | hogei mila |
    | 30000 | hogeita hamar mila |
    | 21000 | hogeita bat mila |

# NTS-27: Euskaltzaindia, rule 7 "Zenbakien idazkeraz" (1994): table (600 seiehun, 1200 mila eta berrehun,
# 1201 mila berrehun eta bat, 1984 mila bederatziehun eta laurogeita lau, 1.000.000 milioi bat,
# 10.000.000 hamar milioi, 1.000.000.000 mila milioi) and note 3: from a thousand upwards "eta" joins the
# following part, and is dropped when the hundreds that follow carry their own "eta".
Scenario Outline: NTS-27 Basque cardinals above a thousand follow rule 7
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number     | expected                                   |
    | 600        | seiehun                                    |
    | 1000       | mila                                       |
    | 1001       | mila eta bat                               |
    | 1010       | mila eta hamar                             |
    | 1021       | mila eta hogeita bat                       |
    | 1100       | mila eta ehun                              |
    | 1200       | mila eta berrehun                          |
    | 1201       | mila berrehun eta bat                      |
    | 1984       | mila bederatziehun eta laurogeita lau      |
    | 2000       | bi mila                                    |
    | 2001       | bi mila eta bat                            |
    | 10000      | hamar mila                                 |
    | 21000      | hogeita bat mila                           |
    | 1000000    | milioi bat                                 |
    | 1000001    | milioi bat eta bat                         |
    | 2000000    | bi milioi                                  |
    | 10000000   | hamar milioi                               |
    | 1000000000 | mila milioi                                |
    | -1001      | ken mila eta bat                           |

# NTS-30F: Euskaltzaindia rule 203 (2025) §4: Basque uses the long scale and the multiplier of milioi is one
# cardinal of up to six digits, with its own "eta" ("berrehun eta sei mila zazpiehun eta laurogeita hamabost
# milioi"); never "bi mila milioi eta bostehun milioi".
Scenario Outline: NTS-30F Basque million multipliers of four to six digits
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number       | expected                                                                                          |
    | 1500000000   | mila eta bostehun milioi                                                                          |
    | 2500000000   | bi mila eta bostehun milioi                                                                       |
    | 1001000000   | mila eta bat milioi                                                                               |
    | 206795000000 | berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi                                    |
    | 206795843000 | berrehun eta sei mila zazpiehun eta laurogeita hamabost milioi zortziehun eta berrogeita hiru mila |

Scenario Outline: Irregular first ordinal
    Given I use the "EU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | lehenengo |

Scenario Outline: Suffixed ordinal numbers
    Given I use the "EU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 |  | bigarren |
    | 3 |  | hirugarren |
    | 10 |  | hamargarren |
    | 11 |  | hamaikagarren |
    | 20 |  | hogeigarren |
    | 21 |  | hogeita batgarren |
    | 1000 |  | milagarren |
    | 5 |  | bosgarren |
    | 15 |  | hamabosgarren |
    | 25 |  | hogeita bosgarren |
    | 75 |  | hirurogeita hamabosgarren |
    | 100 |  | ehungarren |
    | 101 |  | ehun eta batgarren |
    | 500 |  | bostehungarren |
    | 600 |  | seiehungarren |

# NTS-08 validation: Euskaltzaindia, rule 18 "Ordinalen eta banatzaileen idazkera": bigarren,
# hirugarren, laugarren, bosgarren (not bostgarren), hamabosgarren, hogeita batgarren, hogeita
# bosgarren, hirurogeita hamabosgarren, ehungarren, milagarren. Rule 7 "Zenbakien idazkeraz" gives
# only "zero"/"huts" for 0. NTS-38: rule 203 §5 settles the ordinals of the whole cardinal domain (to
# 999 999 999 999); zero (rule 203 lists "zerogarren", NTS-50) and the values beyond the cardinal fail closed.
Scenario Outline: Basque ordinals outside the validated domain are not supported
    Given I use the "EU" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 0 |
    | 1000000000000 |

# NTS-28: Euskaltzaindia rule 18: the ordinal is the cardinal written in letters followed by -garren ("…
# ehungarren, milagarren …"), batgarren separate from the preceding part; on the NTS-27 cardinal (rule 7) the
# ordinal covers 1-999 999 999. NTS-38: Euskaltzaindia rule 203 §5 reduces the round million to its noun
# ("1 000 000. milioigarren", "2 000 000. bi milioigarren", "1 000 001.: milioi bat eta batgarren") and its
# general rule extends the domain to the milliard, "mila milioi" since NTS-30F.
Scenario Outline: NTS-28 Basque ordinals above a thousand
    Given I use the "EU" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | mila eta batgarren |
    | 1201 | mila berrehun eta batgarren |
    | 2000 | bi milagarren |
    | 2005 | bi mila eta bosgarren |
    | 1000000 | milioigarren |
    | 2000000 | bi milioigarren |
    | 1000001 | milioi bat eta batgarren |
    | 1000000000 | mila milioigarren |
    | 1000000001 | mila milioi eta batgarren |

Scenario: Ordinal conversion is supported
    Given I use the "EU" number converter
    Then the converter supports ordinal conversion

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "EU" number converter
    Then the converter supports clock-time conversion

# Sareko Euskal Gramatika (EHU), "Orduak nola eman euskaraz": whole hours are definite
# ("ordu bata", "ordu biak", "hirurak"); halves use the bare numeral ("ordu bat eta erdiak",
# "*ordu bata eta erdiak" is marked incorrect; "hiru eta erdiak"); "ordu bata eta laurden";
# "bostak laurden gutxi". "ordu" is only used with one and two. These clock-case forms are literal
# per hour and do not change the cardinals ("bat", "bi").
Scenario Outline: Idiomatic Basque clock times
    Given I use the "EU" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ordu bata |
    | 01:15 | ordu bata eta laurden |
    | 01:30 | ordu bat eta erdiak |
    | 01:45 | ordu biak laurden gutxi |
    | 02:00 | ordu biak |
    | 02:30 | ordu bi eta erdiak |
    | 03:00 | hirurak |
    | 03:30 | hiru eta erdiak |
    | 04:45 | bostak laurden gutxi |
    | 12:00 | hamabiak |
    | 12:45 | ordu bata laurden gutxi |
    | 13:30 | ordu bat eta erdiak |
    | 04:00 | laurak |
    | 05:00 | bostak |
    | 06:00 | seiak |
    | 07:00 | zazpiak |
    | 08:00 | zortziak |
    | 09:00 | bederatziak |
    | 10:00 | hamarrak |
    | 11:00 | hamaikak |
    | 00:00 | hamabiak |
    | 00:15 | hamabiak eta laurden |
    | 11:45 | hamabiak laurden gutxi |
    | 12:15 | hamabiak eta laurden |
    | 23:45 | hamabiak laurden gutxi |

Scenario Outline: Basque clock forms do not change the cardinals
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | bat |
    | 2 | bi |
