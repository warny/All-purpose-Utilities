@NumberToString
Feature: Greek number conversion

Scenario Outline: DecimalTest 1
    Given I use the "EL" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | ένα κόμμα πέντε |
    | 12.34 | δώδεκα κόμμα τρία τέσσερα |

Scenario Outline: Cardinals_Basic 2
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

Scenario Outline: Ordinals_GenderForms 3
    Given I use the "EL" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | πρώτος |
    | 2 | δεύτερος |
    | 10 | δέκατος |
    | 11 | ενδέκατος |

Scenario Outline: Ordinals_GenderForms 4
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

Scenario Outline: Ordinals_GenderForms 5
    Given I use the "EL" number converter
    And I use the variants "gender=ουδέτερο"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | πρώτο |
    | 11 | ενδέκατο |
