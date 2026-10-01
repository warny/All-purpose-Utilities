@NumberToString @SK
Feature: Slovak number conversion

Background:
    Given I use the "SK" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nula |
    | 1 | jeden |
    | 2 | dva |
    | 3 | tri |
    | 9 | deväť |
    | 10 | desať |
    | 11 | jedenásť |
    | 12 | dvanásť |
    | 19 | devätnásť |
    | 20 | dvadsať |
    | 21 | dvadsať jeden |
    | 100 | sto |
    | 101 | sto jeden |
    | 200 | dvesto |
    | 300 | tristo |
    | 1000 | tisíc |
    | 2000 | dva tisíc |
    | 10000 | desať tisíc |
    | -1 | mínus jeden |

Scenario: The regional alias uses Slovak wording
    Then the "SK" and "SK-SK" converters produce the same cardinal wording for 2

Scenario: Ordinal conversion is supported
    Given I use the "SK" number converter
    Then the converter supports ordinal conversion

# Tens and units are ordinal ("dvadsiaty prvý"); hundreds and thousands stay cardinal, written as
# one word with a following unit ("stoprvý", "dvetisícdruhý") and apart before tens ("päťsto
# dvadsiaty ôsmy"), per the JÚĽŠ SAV spelling rules summarized by teraz.sk. After a long syllable
# the ending is short (rhythmic law): "piaty", "piata", "tisíci".
Scenario Outline: Slovak ordinal numbers
    Given I use the "SK" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | prvý |
    | 2 | druhý |
    | 3 | tretí |
    | 4 | štvrtý |
    | 5 | piaty |
    | 10 | desiaty |
    | 11 | jedenásty |
    | 20 | dvadsiaty |
    | 21 | dvadsiaty prvý |
    | 100 | stý |
    | 101 | stoprvý |
    | 121 | sto dvadsiaty prvý |
    | 200 | dvojstý |
    | 528 | päťsto dvadsiaty ôsmy |
    | 1000 | tisíci |
    | 1001 | tisícprvý |
    | 2002 | dvetisícdruhý |

Scenario Outline: Slovak ordinals agree in gender and case
    Given I use the "SK" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 5 | gender=ženský | piata |
    | 3 | gender=ženský | tretia |
    | 2 | gender=ženský,case=genitív | druhej |
    | 3 | gender=ženský,case=genitív | tretej |
    | 2 | gender=stredný | druhé |
    | 1 | gender=mužský,case=genitív | prvého |

Scenario Outline: Slovak gendered cardinals
    Given I use the "SK" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | jeden |
    | 1 | gender=ženský | jedna |
    | 2 | gender=ženský | dve |
    | 1 | gender=stredný | jedno |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "SK" number converter
    Then the converter supports clock-time conversion

# "štvrť na dve", "pol druhej" (feminine genitive ordinal of the following hour, except
# "pol jednej"), "tri štvrte na dve".
Scenario Outline: Idiomatic Slovak clock times
    Given I use the "SK" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | jedna hodina |
    | 01:15 | štvrť na dve |
    | 01:30 | pol druhej |
    | 01:45 | tri štvrte na dve |
    | 02:00 | dve hodiny |
    | 02:30 | pol tretej |
    | 05:00 | päť hodín |
    | 12:30 | pol jednej |
    | 13:30 | pol druhej |
