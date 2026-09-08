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
