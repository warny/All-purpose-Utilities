@NumberToString @SK
Feature: Slovak number conversion

Background:
    Given I use the "SK" number converter

# NTS-30B, Pravidlá slovenského pravopisu (JÚĽŠ SAV): tens with units, hundreds and thousands are one word
# (dvadsaťsedem, päťsto, dvetisíc, dvadsaťsedemtisíc); the thousands, hundreds and tens-with-units are
# written apart for readability (sedemtisíc dvesto osemdesiattri); millions and milliards are apart and agree
# (štyri milióny, päť miliónov, tri miliardy; KSSJ: miliarda -y -iárd). NTS-30C: the generated names agree
# the same way by suffix family (sedem biliónov, PSP; jedna biliarda, dve biliardy, päť biliárd).
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
    | 21 | dvadsaťjeden |
    | 22 | dvadsaťdva |
    | 27 | dvadsaťsedem |
    | 100 | sto |
    | 101 | sto jeden |
    | 200 | dvesto |
    | 300 | tristo |
    | 400 | štyristo |
    | 500 | päťsto |
    | 1000 | tisíc |
    | 2000 | dvetisíc |
    | 3000 | tritisíc |
    | 5000 | päťtisíc |
    | 10000 | desaťtisíc |
    | 27000 | dvadsaťsedemtisíc |
    | 7283 | sedemtisíc dvesto osemdesiattri |
    | 1000000 | jeden milión |
    | 2000000 | dva milióny |
    | 5000000 | päť miliónov |
    | 22000000 | dvadsaťdva miliónov |
    | 2000000000 | dve miliardy |
    | 5000000000 | päť miliárd |
    | 1000000000000 | jeden bilión |
    | 2000000000000 | dva bilióny |
    | 5000000000000 | päť biliónov |
    | 1000000000000000 | jedna biliarda |
    | 2000000000000000 | dve biliardy |
    | 5000000000000000 | päť biliárd |
    | 15348848 | pätnásť miliónov tristoštyridsaťosemtisíc osemsto štyridsaťosem |
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
