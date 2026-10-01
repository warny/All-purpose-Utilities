@NumberToString @CS
Feature: Czech number conversion

Background:
    Given I use the "CS" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nula |
    | 1 | jedna |
    | 2 | dva |
    | 3 | tři |
    | 9 | devět |
    | 10 | deset |
    | 11 | jedenáct |
    | 12 | dvanáct |
    | 19 | devatenáct |
    | 20 | dvacet |
    | 21 | dvacet jedna |
    | 100 | sto |
    | 200 | dvě stě |
    | 300 | tři sta |
    | 500 | pět set |
    | 1000 | tisíc |
    | 2000 | dva tisíc |
    | 10000 | deset tisíc |
    | 1000000 | jedna milion |
    | 2000000 | dva milion |
    | 1000000000 | jedna miliard |
    | 1000000000000 | jedna bilion |
    | 1000000000000000 | jedna biliard |
    | 1000000000000000000 | jedna trilion |

Scenario: The regional alias uses Czech wording
    Then the "CS" and "CS-CZ" converters produce the same cardinal wording for 2

Scenario: Ordinal conversion is supported
    Given I use the "CS" number converter
    Then the converter supports ordinal conversion

# Every component of a Czech compound ordinal is ordinal ("dvacátý první", "stý dvacátý první").
Scenario Outline: Czech ordinal numbers
    Given I use the "CS" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | první |
    | 2 | druhý |
    | 3 | třetí |
    | 4 | čtvrtý |
    | 5 | pátý |
    | 9 | devátý |
    | 10 | desátý |
    | 11 | jedenáctý |
    | 19 | devatenáctý |
    | 20 | dvacátý |
    | 21 | dvacátý první |
    | 40 | čtyřicátý |
    | 100 | stý |
    | 101 | stý první |
    | 121 | stý dvacátý první |
    | 300 | třístý |
    | 1000 | tisící |
    | 1001 | tisící první |
    | 2000 | dvoutisící |

Scenario Outline: Czech ordinals agree in gender and case
    Given I use the "CS" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 | gender=mužský | druhý |
    | 2 | rod=ženský | druhá |
    | 2 | gender=střední | druhé |
    | 1 | gender=ženský | první |
    | 2 | gender=ženský,case=genitiv | druhé |
    | 2 | gender=ženský,case=akuzativ | druhou |
    | 3 | gender=ženský,case=genitiv | třetí |
    | 21 | gender=ženský | dvacátá první |
    | 2 | gender=mužský,pád=genitiv | druhého |

Scenario Outline: Czech cardinals keep the counting form by default
    Given I use the "CS" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | jedna |
    | 2 |  | dva |
    | 1 | gender=mužský | jeden |
    | 2 | gender=ženský | dvě |
    | 1 | gender=střední | jedno |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "CS" number converter
    Then the converter supports clock-time conversion

# Internetová jazyková příručka (ÚJČ AV ČR), "Časové údaje": "čtvrt na dvě", "půl druhé",
# "tři čtvrtě na dvě". The half hour uses the feminine genitive ORDINAL of the following hour,
# except "půl jedné"; the quarters use the feminine accusative cardinal ("na jednu", "na dvě").
Scenario Outline: Idiomatic Czech clock times
    Given I use the "CS" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | jedna hodina |
    | 01:15 | čtvrt na dvě |
    | 01:30 | půl druhé |
    | 01:45 | tři čtvrtě na dvě |
    | 02:00 | dvě hodiny |
    | 02:30 | půl třetí |
    | 05:00 | pět hodin |
    | 12:15 | čtvrt na jednu |
    | 12:30 | půl jedné |
    | 13:30 | půl druhé |
