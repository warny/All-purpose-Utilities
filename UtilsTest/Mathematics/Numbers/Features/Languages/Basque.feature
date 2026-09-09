@NumberToString @EU
Feature: Basque number conversion

Scenario Outline: Cardinal numbers in the twenties and higher
    Given I use the "eu-ES" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | hogeita bat |
    | 31 | hogeita hamaika |
    | 38 | hogeita hemezortzi |
    | 57 | berrogeita hamazazpi |

Scenario Outline: Hundreds
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 100 | ehun |
    | 205 | berrehun eta bost |

Scenario Outline: Decimal numbers
    Given I use the "EU-es" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | bat koma bost |

Scenario Outline: Thousands
    Given I use the "EU" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | mila |
    | 20000 | hogei mila |
    | 30000 | hogeita hamar mila |
    | 21000 | hogeita bat mila |

Scenario Outline: Irregular first ordinal
    Given I use the "EU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | lehenengo |

Scenario Outline: Suffixed ordinal numbers
    Given I use the "EU" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 2 |  | bigarren |
    | 3 |  | hirugarren |
    | 10 |  | hamargarren |
    | 11 |  | hamaikagarren |
    | 20 |  | hogeigarren |
    | 21 |  | hogeita batgarren |
    | 1000 |  | milagarren |
