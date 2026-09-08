@NumberToString
Feature: Finnish number conversion

Scenario Outline: Cardinals_Basic 1
    Given I use the "FI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | nolla |
    | 1 | yksi |
    | 2 | kaksi |
    | 3 | kolme |
    | 9 | yhdeksän |
    | 10 | kymmenen |
    | 11 | yksitoista |
    | 12 | kaksitoista |
    | 19 | yhdeksäntoista |
    | 20 | kaksikymmentä |
    | 21 | kaksikymmentä yksi |
    | 100 | sata |
    | 200 | kaksisataa |
    | 101 | sata yksi |

Scenario Outline: Cardinals_Thousands 2
    Given I use the "FI" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | tuhat |
    | 2000 | kaksi tuhat |

Scenario Outline: Ordinals_Basic 3
    Given I use the "FI" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ensimmäinen |
    | 2 | toinen |
    | 3 | kolmas |
    | 10 | kymmenes |
    | 11 | yhdestoista |
    | 12 | kahdestoista |
    | 20 | kahdeskymmenes |
    | 100 | sadas |
    | 1000 | tuhannes |

Scenario Outline: Variants_Partitiivi 4
    Given I use the "FI" number converter
    And I use the variants "case=partitiivi"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | yhtä |
    | 2 | kahta |
    | 3 | kolmea |
    | 10 | kymmentä |
    | 100 | sataa |
    | 1000 | tuhatta |

Scenario Outline: Variants_Genetiivi 5
    Given I use the "FI" number converter
    And I use the variants "case=genetiivi"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | yhden |
    | 2 | kahden |
    | 100 | sadan |
    | 1000 | tuhannen |
