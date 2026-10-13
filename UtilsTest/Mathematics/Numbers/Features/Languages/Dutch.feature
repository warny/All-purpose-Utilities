@NumberToString @NL
Feature: Dutch number conversion

Scenario Outline: Decimal numbers
    Given I use the "NL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | een komma vijf |
    | 12.34 | twaalf komma drie vier |
    # NTS-30A: a decimal digit 0 is read "nul" (leading, trailing, internal).
    | 21.05 | eenentwintig komma nul vijf |
    | 21.50 | eenentwintig komma vijf nul |
    | 21.505 | eenentwintig komma vijf nul vijf |
    | 21.005 | eenentwintig komma nul nul vijf |
    | 0.05 | nul komma nul vijf |
    | -21.05 | min eenentwintig komma nul vijf |

Scenario Outline: Basic cardinal numbers
    Given I use the "NL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | een |
    | 11 | elf |
    | 12 | twaalf |
    | 20 | twintig |
    | 21 | eenentwintig |
    | 100 | honderd |
    | 1000 | duizend |

Scenario Outline: Irregular and suffixed ordinal numbers
    Given I use the "NL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | eerste |
    | 2 | tweede |
    | 3 | derde |
    | 4 | vierde |
    | 5 | vijfde |
    | 10 | tiende |
    | 20 | twintigste |
    | 100 | honderdste |

Scenario Outline: Additional ordinal numbers for units and teens
    Given I use the "NL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 6 |  | zesde |
    | 7 |  | zevende |
    | 8 |  | achtste |
    | 9 |  | negende |
    | 11 |  | elfde |
    | 12 |  | twaalfde |
    | 13 |  | dertiende |
    | 19 |  | negentiende |

Scenario Outline: Tens and compound ordinal numbers
    Given I use the "NL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | eenentwintigste |
    | 101 |  | honderdeerste |

# NTS-08 validation: Taaladvies.net (Taalunie), "Aaneenschrijven van telwoorden (algemeen)": numbers
# are written as one word up to and including duizend (honderdeen, tweehonderdvijfendertig,
# zesduizend, achtentwintigduizend, driehonderdeenentachtigduizend), with a space after duizend
# (vierduizend twee, zesduizend achtentwintig); an ordinal keeps the spelling of its cardinal and
# adds -ste, -de or -e (honderdste, honderdvijfendertigste, drieduizend tweehonderdtwintigste).
# Woordenlijst Nederlandse Taal: nulde, tweeëntwintigste, tweeduizendste, honderdeerste (rangtelwoord).
Scenario Outline: Dutch cardinals are written as one word up to duizend
    Given I use the "NL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 22 | tweeëntwintig |
    | 23 | drieëntwintig |
    | 43 | drieënveertig |
    | 101 | honderdeen |
    | 135 | honderdvijfendertig |
    | 235 | tweehonderdvijfendertig |
    | 1001 | duizend een |
    | 2000 | tweeduizend |
    | 4002 | vierduizend twee |
    | 6028 | zesduizend achtentwintig |
    | 28064 | achtentwintigduizend vierenzestig |
    | 100000 | honderdduizend |
    | 271850 | tweehonderdeenenzeventigduizend achthonderdvijftig |
    | 381000 | driehonderdeenentachtigduizend |

# NTS-31: the long scale. Taaladvies.net (Taalunie), "Aaneenschrijven van telwoorden (algemeen)": "Miljoen en
# miljard zijn altijd aparte woorden" (drie miljoen, vijf miljard tweehonderd miljoen, driehonderdzevenenveertig
# miljard zeshonderdvijfentwintig miljoen zevenhonderdachtentwintigduizend tweehonderdeenentwintig): the scale noun
# stays singular after the numeral, the groups are separated by a space without "en". Onze Taal 70 (2001), "Een
# miljard of een biljoen?": "Een miljoen miljoen is een biljoen; een miljoen biljoen is een triljoen", "duizend
# miljoen is een miljard; duizend biljoen is een biljard" (-iljoen/-iljard; "een" kept before miljoen, unlike
# duizend). nl.wikipedia "Lijst van machten van tien": quadriljoen, quintiljoen, sextiljoen, octiljoen (Latin
# spelling). Higher names are the productive Conway-Guy-Wechsler continuation (repository rule).
Scenario Outline: NTS-31 Dutch long-scale cardinals
    Given I use the "NL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | duizend |
    | 2000 | tweeduizend |
    | 999999 | negenhonderdnegenennegentigduizend negenhonderdnegenennegentig |
    | 1000000 | een miljoen |
    | 2000000 | twee miljoen |
    | 21000000 | eenentwintig miljoen |
    | 1001000 | een miljoen duizend |
    | 1000001 | een miljoen een |
    | 1234567 | een miljoen tweehonderdvierendertigduizend vijfhonderdzevenenzestig |
    | 3400000 | drie miljoen vierhonderdduizend |
    | 3004000 | drie miljoen vierduizend |
    | 3000400 | drie miljoen vierhonderd |
    | 3000004 | drie miljoen vier |
    | 17053980 | zeventien miljoen drieënvijftigduizend negenhonderdtachtig |
    | 100000000 | honderd miljoen |
    | 1000000000 | een miljard |
    | 2001000000 | twee miljard een miljoen |
    | 4000000000 | vier miljard |
    | 5200000000 | vijf miljard tweehonderd miljoen |
    | 347625728221 | driehonderdzevenenveertig miljard zeshonderdvijfentwintig miljoen zevenhonderdachtentwintigduizend tweehonderdeenentwintig |
    | 1000000000000 | een biljoen |
    | 3000000000000 | drie biljoen |
    | 1000000000000000 | een biljard |
    | 1000000000000000000 | een triljoen |
    | 1000000000000000000000 | een triljard |
    | 1000000000000000000000000 | een quadriljoen |
    | 1000000000000000000000000000 | een quadriljard |
    | 1000000000000000000000000000000 | een quintiljoen |
    | 1000000000000000000000000000000000000 | een sextiljoen |
    | 1000000000000000000000000000000000000000000000000 | een octiljoen |
    | 1000000000000000000000000000000000000000000000000000000000000 | een deciljoen |
    | -2000000 | min twee miljoen |

# NTS-31: ordinals from a million. e-ANS (Algemene Nederlandse Spraakkunst), rangtelwoorden: the ordinal keeps the
# spelling structure of the cardinal and only its final element becomes ordinal; the scale nouns take -ste
# (miljoen → miljoenste, miljard → miljardste). Besluit bekendmaking spellingvoorschriften 2005, rule 6.N: "de twee
# miljoenste", "de twee miljoen tweehonderdduizendste", "de twee miljoen tweehonderdduizend tweehonderdtweeëntwintigste":
# the multiplier stays a separate cardinal, the earlier groups stay cardinal, no "en" between the groups. A round
# value with the multiplier one is the scale noun alone (miljoenste, not een miljoenste).
Scenario Outline: NTS-31 Dutch round scale ordinals
    Given I use the "NL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000000 | miljoenste |
    | 2000000 | twee miljoenste |
    | 21000000 | eenentwintig miljoenste |
    | 100000000 | honderd miljoenste |
    | 1000000000 | miljardste |
    | 2000000000 | twee miljardste |
    | 1000000000000 | biljoenste |
    | 1000000000000000 | biljardste |
    | 1000000000000000000 | triljoenste |
    | 2000000000000000000 | twee triljoenste |
    | -1000000 | min miljoenste |

Scenario Outline: NTS-31 Dutch compound ordinals keep the cardinal groups and ordinalize the final element
    Given I use the "NL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001000 | een miljoen duizendste |
    | 1000001 | een miljoen eerste |
    | 2200000 | twee miljoen tweehonderdduizendste |
    | 2200022 | twee miljoen tweehonderdduizend tweeëntwintigste |
    | 2200222 | twee miljoen tweehonderdduizend tweehonderdtweeëntwintigste |
    | 2001000000 | twee miljard een miljoenste |
    | 9223372036854775807 | negen triljoen tweehonderddrieëntwintig biljard driehonderdtweeënzeventig biljoen zesendertig miljard achthonderdvierenvijftig miljoen zevenhonderdvijfenzeventigduizend achthonderdzevende |

Scenario Outline: Dutch ordinals keep the spelling of the cardinal
    Given I use the "NL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nulde |
    | 8 | achtste |
    | 22 | tweeëntwintigste |
    | 102 | honderdtweede |
    | 103 | honderdderde |
    | 108 | honderdachtste |
    | 111 | honderdelfde |
    | 135 | honderdvijfendertigste |
    | 1000 | duizendste |
    | 2000 | tweeduizendste |
    | 3220 | drieduizend tweehonderdtwintigste |
    | 21000 | eenentwintigduizendste |
    | 999999 | negenhonderdnegenennegentigduizend negenhonderdnegenennegentigste |

Scenario: Ordinal conversion is supported
    Given I use the "NL" number converter
    Then the converter supports ordinal conversion

Scenario Outline: Year wording
    Given I use the "NL" number converter
    When I convert the year <year>
    Then the result is "<expected>"

Examples:
    | year | expected                  |
    | 1984 | negentien vierentachtig   |
    | 1900 | negentien honderd         |
    | 1100 | elf honderd               |

Scenario: Fraction connector wording
    Given I use the "NL" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "drie op twee"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "NL" number converter
    Then the converter supports clock-time conversion

# Taaladvies.net "Kloktijden": quarters of the hour read "over <h>", "voor half <h+1>",
# "over half <h+1>", "voor <h+1>". The whole hour keeps the distinctive accent ("één uur",
# Taaladvies "Klemtoonteken"), which is not needed once "een" cannot be read as the article.
# "half twee" (two words) is retained; Taaladvies accepts both "half twee" and "halftwee".
Scenario Outline: Idiomatic Dutch clock times
    Given I use the "NL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | één uur |
    | 02:00 | twee uur |
    | 01:05 | vijf over een |
    | 01:10 | tien over een |
    | 01:15 | kwart over een |
    | 01:20 | tien voor half twee |
    | 01:25 | vijf voor half twee |
    | 01:30 | half twee |
    | 01:35 | vijf over half twee |
    | 01:40 | tien over half twee |
    | 01:45 | kwart voor twee |
    | 01:50 | tien voor twee |
    | 01:55 | vijf voor twee |
    | 12:00 | twaalf uur |
    | 13:30 | half twee |
    | 12:45 | kwart voor een |

Scenario Outline: Dutch clock times round to the nearest five minutes and carry past midnight
    Given I use the "NL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:27 | vijf voor half twee |
    | 01:28 | half twee |
    | 23:58 | twaalf uur |

Scenario: Clock wording does not change Dutch cardinals
    Given I use the "NL" number converter
    When I convert the cardinal number 1
    Then the result is "een"
