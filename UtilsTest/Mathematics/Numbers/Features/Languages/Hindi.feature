@NumberToString @HI
Feature: Hindi number conversion

Scenario Outline: Decimal numbers
    Given I use the "HI" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | एक दशमलव पांच |
    | 12.34 | बारह दशमलव तीन चार |

Scenario Outline: Irregular and suffixed ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | पहला |
    | 2 |  | दूसरा |
    | 3 |  | तीसरा |
    | 4 |  | चौथा |
    | 5 |  | पांचवाँ |
    | 6 |  | छठा |
    | 7 |  | सातवाँ |
    | 11 |  | ग्यारहवाँ |
    | 20 |  | बीसवाँ |

Scenario Outline: Irregular feminine ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=strī | पहली |
    | 2 | gender=strī | दूसरी |
    | 3 | gender=strī | तीसरी |
    | 4 | gender=strī | चौथी |

Scenario Outline: Feminine suffixed ordinal numbers
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 6 | gender=strī | छठी |
    | 5 | gender=strī | पांचवीं |
    | 7 | gender=strī | सातवीं |

Scenario Outline: Masculine ordinal numbers masculine unchanged
    Given I use the "HI" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | पहला |
    | 6 |  | छठा |
    | 7 |  | सातवाँ |

Scenario: Ordinal conversion is supported
    Given I use the "HI" number converter
    Then the converter supports ordinal conversion
