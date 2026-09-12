@NumberToString @HE
Feature: Hebrew number conversion

Scenario Outline: Decimal numbers
    Given I use the "HE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | אחד נקודה חמש |
    | 12.34 | עשר שתיים נקודה שלוש ארבע |

Scenario Outline: Basic cardinal numbers
    Given I use the "HE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | אחד |
    | 2 | שתיים |
    | 3 | שלוש |
    | 10 | עשר |
    | 20 | עשרים |
    | 100 | מאה |
    | 200 | מאתיים |
    | 1000 | אחד אלף |

Scenario Outline: Masculine cardinal numbers
    Given I use the "HE" number converter
    And I use the variants "gender=zachar"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 3 | שלושה |
    | 4 | ארבעה |
    | 5 | חמישה |
    | 6 | שישה |
    | 10 | עשרה |

Scenario Outline: Feminine cardinal numbers
    Given I use the "HE" number converter
    And I use the variants "gender=nekeva"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | אחת |

Scenario Outline: Irregular ordinal numbers
    Given I use the "HE" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ראשון |
    | 2 | שני |
    | 3 | שלישי |
    | 10 | עשירי |

Scenario Outline: Feminine ordinal numbers
    Given I use the "HE" number converter
    And I use the variants "gender=nekeva"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ראשונה |
    | 2 | שנייה |
    | 3 | שלישית |
    | 10 | עשירית |

Scenario: Ordinal above the configured range falls back to cardinal wording
    Given I use the "HE" number converter
    When I convert the ordinal number 20
    Then the result is "עשרים"

Scenario: Ordinal conversion is supported
    Given I use the "HE" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "HE" number converter
    When I convert the fraction 3/2
    Then the result is "שלוש על שתיים"
