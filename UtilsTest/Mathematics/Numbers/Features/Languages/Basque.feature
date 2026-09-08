@NumberToString
Feature: Basque number conversion

Scenario Outline: BasqueTensAdjustments 1
    Given I use the "eu-ES" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | hogeita bat |
    | 31 | hogeita hamaika |
    | 38 | hogeita hemezortzi |
    | 57 | berrogeita hamazazpi |

Scenario Outline: BasqueHundreds 2
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | ehun |
    | 205 | berrehun eta bost |

Scenario Outline: BasqueDecimal 3
    Given I use the "EU-es" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | bat koma bost |

Scenario Outline: BasqueThousandsStillApplyTargetedReplacements 4
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | mila |
    | 20000 | hogei mila |
    | 30000 | hogeita hamar mila |
    | 21000 | hogeita bat mila |
