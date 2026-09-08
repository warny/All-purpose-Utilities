@NumberToString @FA
Feature: Persian number conversion

Background:
    Given I use the "FA" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | صفر |
    | 1 | یک |
    | 2 | دو |
    | 3 | سه |
    | 9 | نه |
    | 10 | ده |
    | 11 | یازده |
    | 12 | دوازده |
    | 19 | نوزده |
    | 20 | بیست |
    | 21 | بیست و یک |
    | 30 | سی |
    | 100 | صد |
    | 101 | صد و یک |
    | 200 | دویست |
    | 1000 | هزار |
    | 2000 | دو هزار |
    | 10000 | ده هزار |
    | 1000000 | یک میلیون |
    | 1000000000 | یک میلیارد |
    | 1000000000000 | یک تریلیون |
    | -1 | منفی یک |
    | -10 | منفی ده |
