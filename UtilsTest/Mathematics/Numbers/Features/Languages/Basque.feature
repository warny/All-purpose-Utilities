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
