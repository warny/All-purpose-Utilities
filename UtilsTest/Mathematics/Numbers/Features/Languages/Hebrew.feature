@NumberToString
Feature: Hebrew number conversion

Scenario Outline: DecimalTest 1
    Given I use the "HE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | אחד נקודה חמש |
    | 12.34 | עשר שתיים נקודה שלוש ארבע |

Scenario Outline: Cardinals_Basic 2
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

Scenario Outline: Cardinals_GenderVariants 3
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

Scenario Outline: Cardinals_GenderVariants 4
    Given I use the "HE" number converter
    And I use the variants "gender=nekeva"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | אחת |

Scenario Outline: Ordinals_Exceptions 5
    Given I use the "HE" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ראשון |
    | 2 | שני |
    | 3 | שלישי |
    | 10 | עשירי |

Scenario Outline: Ordinals_Exceptions 6
    Given I use the "HE" number converter
    And I use the variants "gender=nekeva"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ראשונה |
    | 2 | שנייה |
    | 3 | שלישית |
