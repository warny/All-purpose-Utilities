@NumberToString @HI
Feature: Hindi number conversion

# Hindi cardinals 21-99 are lexicalized, not composed (NTS-10): "इक्कीस", never "बीस एक".
# Sources: Wiktionary Module:number_list/data/hi, Unicode CLDR RBNF hi (spellout-cardinal) and a
# Hindi school counting list (schooldekho.org). Where the sources disagree the majority spelling
# is used: 44 चौवालीस (CLDR, school; Wiktionary चवालीस), 53 तिरपन and 63 तिरसठ (Wiktionary,
# school; CLDR तिरेपन/तिरेसठ), 79 उन्यासी (Wiktionary, school; CLDR उनासी), 91-99 in -नवे
# (Wiktionary, school; CLDR -नबे), 95 पंचानवे (Wiktionary; stem shared with CLDR पंचानबे).
Scenario Outline: Lexicalized cardinals 20-99
    Given I use the "HI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 20 | बीस |
    | 21 | इक्कीस |
    | 22 | बाईस |
    | 29 | उनतीस |
    | 31 | इकतीस |
    | 37 | सैंतीस |
    | 42 | बयालीस |
    | 44 | चौवालीस |
    | 48 | अड़तालीस |
    | 53 | तिरपन |
    | 57 | सत्तावन |
    | 63 | तिरसठ |
    | 64 | चौंसठ |
    | 68 | अड़सठ |
    | 73 | तिहत्तर |
    | 79 | उन्यासी |
    | 88 | अट्ठासी |
    | 91 | इक्यानवे |
    | 95 | पंचानवे |
    | 99 | निन्यानवे |

# The lexicalized forms are reused inside larger numbers. 1021 keeps the configuration's existing
# bare "हज़ार" for one thousand (Wiktionary's headword; not re-audited by NTS-10).
Scenario Outline: Lexicalized cardinals inside larger numbers
    Given I use the "HI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 121 | एक सौ इक्कीस |
    | 221 | दो सौ इक्कीस |
    | 1021 | हज़ार इक्कीस |
    | 2021 | दो हज़ार इक्कीस |
    | 21000 | इक्कीस हज़ार |
    | 99999 | निन्यानवे हज़ार नौ सौ निन्यानवे |

# The ordinal suffix attaches to the lexicalized cardinal (इक्कीसवाँ, Wiktionary ordinal forms).
Scenario Outline: Ordinals of lexicalized cardinals
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | इक्कीसवाँ |
    | 99 |  | निन्यानवेवाँ |
    | 121 |  | एक सौ इक्कीसवाँ |
    | 21 | gender=strī | इक्कीसवीं |

Scenario Outline: Decimal numbers
    Given I use the "HI" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | एक दशमलव पांच |
    | 12.34 | बारह दशमलव तीन चार |

Scenario Outline: Irregular and suffixed ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | पहला |
    | 2 |  | दूसरा |
    | 3 |  | तीसरा |
    | 4 |  | चौथा |
    | 5 |  | पांचवाँ |
    | 6 |  | छठा |
    | 7 |  | सातवाँ |
    | 11 |  | ग्यारहवाँ |
    | 20 |  | बीसवाँ |

Scenario Outline: Irregular feminine ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=strī | पहली |
    | 2 | gender=strī | दूसरी |
    | 3 | gender=strī | तीसरी |
    | 4 | gender=strī | चौथी |

Scenario Outline: Feminine suffixed ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 6 | gender=strī | छठी |
    | 5 | gender=strī | पांचवीं |
    | 7 | gender=strī | सातवीं |

# NTS-08 validation: Kamta Prasad Guru, हिंदी व्याकरण, §180: पहला, दूसरा, तीसरा, चौथा, छठा, otherwise -वाँ on the
# last word, also above a hundred ("एक सौ तीनवाँ", "दो सौ आठवाँ"). From 100000 the configured cardinal is
# wrong ("एक सौ हज़ार" instead of "एक लाख"): those ordinals fail closed.
Scenario Outline: Hindi ordinals in the validated domain
    Given I use the "HI" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | एक सौवाँ |
    | 101 | एक सौ एकवाँ |
    | 1000 | हज़ारवाँ |
    | 99999 | निन्यानवे हज़ार नौ सौ निन्यानवेवाँ |

Scenario Outline: Hindi ordinals outside the validated domain are not supported
    Given I use the "HI" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 100000 |
    | 1000000 |

Scenario: Ordinal conversion is supported
    Given I use the "HI" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "HI" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "तीन बटे दो"

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "HI" number converter
    Then the converter supports clock-time conversion

# Hindi fractional clock words: "सवा" (+1/4), "साढ़े" (+1/2), "पौने" (-1/4 of the following hour),
# with the suppletive halves "डेढ़" (1:30) and "ढाई" (2:30). The nukta letter is written in its
# canonical (NFC) decomposed sequence ढ + ़. Three different paths serve 01:30, 02:30 and 03:30.
Scenario Outline: Idiomatic Hindi clock times
    Given I use the "HI" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | एक |
    | 01:15 | सवा एक |
    | 01:30 | डेढ़ |
    | 01:45 | पौने दो |
    | 02:00 | दो |
    | 02:15 | सवा दो |
    | 02:30 | ढाई |
    | 02:45 | पौने तीन |
    | 03:00 | तीन |
    | 03:15 | सवा तीन |
    | 03:30 | साढ़े तीन |
    | 03:45 | पौने चार |
    | 12:30 | साढ़े बारह |
    | 12:45 | पौने एक |
    | 13:30 | डेढ़ |
    | 14:30 | ढाई |
    | 00:00 | बारह |
    | 00:15 | सवा बारह |
    | 11:45 | पौने बारह |
    | 12:00 | बारह |
    | 12:15 | सवा बारह |
    | 23:45 | पौने बारह |
