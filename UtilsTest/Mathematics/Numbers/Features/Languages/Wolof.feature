@NumberToString
Feature: Wolof number conversion

Scenario Outline: DecimalTest 1
    Given I use the "WO" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | benn pojint juróom |
    | 12.34 | fukk ak ñaar pojint ñett ñent |

Scenario Outline: Cardinals_Basic 2
    Given I use the "WO" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | benn |
    | 2 | ñaar |
    | 10 | fukk |
    | 11 | fukk ak benn |
    | 20 | ñaar-fukk |
    | 100 | téeméer |
    | 1000 | benn junni |

Scenario Outline: Ordinals_SuffixAndException 3
    Given I use the "WO" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | bu njëkk |
    | 2 | ñaarël |
    | 10 | fukkël |
