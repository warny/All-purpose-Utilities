@NumberToString @EL
Feature: Greek number conversion

Scenario Outline: Decimal numbers
    Given I use the "EL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | ένα κόμμα πέντε |
    | 12.34 | δώδεκα κόμμα τρία τέσσερα |

Scenario Outline: Basic cardinal numbers
    Given I use the "EL" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | ένα |
    | 3 | τρία |
    | 10 | δέκα |
    | 11 | έντεκα |
    | 20 | είκοσι |
    | 100 | εκατό |
    | 1000 | χίλια |

Scenario Outline: Masculine ordinal numbers
    Given I use the "EL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | πρώτος |
    | 2 | δεύτερος |
    | 10 | δέκατος |
    | 11 | ενδέκατος |

Scenario Outline: Feminine ordinal numbers
    Given I use the "EL" number converter
    And I use the variants "gender=θηλυκό"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | πρώτη |
    | 2 | δεύτερη |
    | 10 | δέκατη |
    | 11 | ενδέκατη |

Scenario Outline: Neuter ordinal numbers
    Given I use the "EL" number converter
    And I use the variants "gender=ουδέτερο"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | πρώτο |
    | 11 | ενδέκατο |

Scenario Outline: Additional ordinal numbers and variants
    Given I use the "EL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | πρώτος |
    | 2 |  | δεύτερος |
    | 3 |  | τρίτος |
    | 10 |  | δέκατος |
    | 11 |  | ενδέκατος |
    | 20 |  | εικοστός |
    | 100 |  | εκατοστός |
    | 1 | gender=θηλυκό | πρώτη |
    | 2 | gender=θηλυκό | δεύτερη |
    | 1 | gender=ουδέτερο | πρώτο |
    | 20 | gender=θηλυκό | εικοστή |

Scenario: Ordinal conversion is supported
    Given I use the "EL" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "EL" number converter
    When I convert the fraction 3/2
    Then the result is "τρία διά δύο"
