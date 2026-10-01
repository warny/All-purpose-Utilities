@NumberToString @TR
Feature: Turkish number conversion

Background:
    Given I use the "TR" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | sıfır |
    | 1 | bir |
    | 2 | iki |
    | 3 | üç |
    | 9 | dokuz |
    | 10 | on |
    | 11 | on bir |
    | 12 | on iki |
    | 19 | on dokuz |
    | 20 | yirmi |
    | 21 | yirmi bir |
    | 100 | yüz |
    | 101 | yüz bir |
    | 200 | iki yüz |
    | 1000 | bin |
    | 2000 | iki bin |
    | 10000 | on bin |
    | 1000000 | bir milyon |
    | 1000000000 | bir milyar |
    | 1000000000000 | bir trilyon |
    | 1000000000000000 | bir katrilyon |
    | 1000000000000000000 | bir kentilyon |
    | -1 | eksi bir |

Scenario: The regional alias uses Turkish wording
    Then the "TR" and "TR-TR" converters produce the same cardinal wording for 2

Scenario: Ordinal conversion is supported
    Given I use the "TR" number converter
    Then the converter supports ordinal conversion

# The ordinal suffix -(I)ncI follows vowel harmony on the last word only ("yirmi birinci");
# "dört" voices to "dördüncü".
Scenario Outline: Turkish ordinal numbers
    Given I use the "TR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | sıfırıncı |
    | 1 | birinci |
    | 2 | ikinci |
    | 3 | üçüncü |
    | 4 | dördüncü |
    | 5 | beşinci |
    | 6 | altıncı |
    | 7 | yedinci |
    | 8 | sekizinci |
    | 9 | dokuzuncu |
    | 10 | onuncu |
    | 11 | on birinci |
    | 20 | yirminci |
    | 21 | yirmi birinci |
    | 40 | kırkıncı |
    | 60 | altmışıncı |
    | 100 | yüzüncü |
    | 101 | yüz birinci |
    | 1000 | bininci |
    | 1001 | bin birinci |
    | 1000000 | bir milyonuncu |
    | 1000000000 | bir milyarıncı |

Scenario Outline: Turkish case forms of the last numeral word
    Given I use the "TR" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 | case=dative | ikiye |
    | 4 | case=dative | dörde |
    | 6 | case=dative | altıya |
    | 12 | case=dative | on ikiye |
    | 1 | case=accusative | biri |
    | 3 | case=accusative | üçü |
    | 10 | case=accusative | onu |
    | 2 | case=nominative | iki |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "TR" number converter
    Then the converter supports clock-time conversion

# TDK usage: "saat yediyi çeyrek geçiyor" (accusative), "saat bir buçuk" (nominative), "saat
# sekize çeyrek var" (dative of the following hour). The first draft of this contract proposed
# "saat bir çeyrek" for 01:15; the normative accusative "saat biri çeyrek geçiyor" is used instead.
Scenario Outline: Idiomatic Turkish clock times
    Given I use the "TR" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | saat bir |
    | 01:15 | saat biri çeyrek geçiyor |
    | 01:30 | saat bir buçuk |
    | 01:45 | saat ikiye çeyrek var |
    | 02:00 | saat iki |
    | 03:45 | saat dörde çeyrek var |
    | 06:15 | saat altıyı çeyrek geçiyor |
    | 11:45 | saat on ikiye çeyrek var |
    | 13:30 | saat bir buçuk |
