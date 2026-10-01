@NumberToString @HI
Feature: Hindi number conversion

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
