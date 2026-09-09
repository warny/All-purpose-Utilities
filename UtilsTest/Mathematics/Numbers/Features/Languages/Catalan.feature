@NumberToString @CA
Feature: Catalan number conversion

Scenario Outline: Catalan cardinal numbers
    Given I use the "CA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 21 | vint-i-un |
    | 105 | cent cinc |
    | 321 | tres-cents vint-i-un |

Scenario Outline: Catalan decimal numbers
    Given I use the "ca-ES" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | un coma cinc |

Scenario Outline: Feminine cardinal numbers
    Given I use the "CA" number converter
    And I use the variants "gender=femení"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | una |
    | 2 | dues |
    | 21 | vint-i-una |
    | 22 | vint-i-dues |
    | 31 | trenta-una |
    | 200 | dues-centes |
    | 201 | dues-centes una |

Scenario Outline: Duration wording
    Given I use the "CA" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | una hora |
    | 02:00:00 | dues hores |
    | 21:00:00 | vint-i-una hores |
    | 22:00:00 | vint-i-dues hores |
    | 00:02:00 | dos minuts |
    | 02:02:00 | dues hores dos minuts |

Scenario Outline: Masculine ordinal numbers
    Given I use the "CA" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 |  | primer |
    | 2 |  | segon |
    | 3 |  | tercer |
    | 4 |  | quart |
    | 5 |  | cinquè |
    | 9 |  | novè |
    | 10 |  | desè |
    | 11 |  | onzè |
    | 19 |  | dinovè |
    | 20 |  | vintè |
    | 30 |  | trentè |

Scenario Outline: Feminine ordinal numbers
    Given I use the "CA" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=femení | primera |
    | 4 | gender=femení | quarta |
    | 5 | gender=femení | cinquena |
    | 19 | gender=femení | dinovena |
    | 20 | gender=femení | vintena |
    | 30 | gender=femení | trentena |

Scenario Outline: Feminine ordinal numbers compound
    Given I use the "CA" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 | gender=femení | vint-i-unena |
    | 22 | gender=femení | vint-i-dosena |

Scenario: Time conversion is supported
    Given I use the "CA" number converter
    Then the converter supports time conversion

Scenario: Ordinal conversion is supported
    Given I use the "CA" number converter
    Then the converter supports ordinal conversion
