@NumberToString @AR
Feature: Arabic number conversion

Scenario Outline: Decimal numbers
    Given I use the "AR" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | واحد فاصل خمسة |
    | 12.34 | عشرة اثنان فاصل ثلاثة أربعة |

Scenario Outline: Basic cardinal numbers
    Given I use the "AR" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | صفر |
    | 1 | واحد |
    | 2 | اثنان |
    | 3 | ثلاثة |
    | 10 | عشرة |
    | 20 | عشرون |
    | 100 | مائة |
    | 1000 | ألف |

Scenario Outline: Feminine cardinal numbers
    Given I use the "AR" number converter
    And I use the variants "gender=muʾannath"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | واحدة |
    | 2 | اثنتان |
    | 3 | ثلاث |
    | 4 | أربع |
    | 5 | خمس |
    | 6 | ست |
    | 7 | سبع |
    | 8 | ثمان |
    | 9 | تسع |
    | 10 | عشر |

Scenario Outline: Masculine ordinal numbers
    Given I use the "AR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | أول |
    | 2 | ثانٍ |
    | 3 | ثالث |
    | 10 | عاشر |
    | 11 | حادي عشر |
    | 12 | ثاني عشر |
    | 19 | تاسع عشر |

Scenario Outline: Feminine ordinal numbers
    Given I use the "AR" number converter
    And I use the variants "gender=muʾannath"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | أولى |
    | 2 | ثانية |
    | 3 | ثالثة |
    | 10 | عاشرة |
    | 11 | حادية عشرة |
    | 19 | تاسعة عشرة |

Scenario Outline: Additional feminine ordinal numbers
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 4 | gender=muʾannath | رابعة |
    | 5 | gender=muʾannath | خامسة |
    | 6 | gender=muʾannath | سادسة |
    | 7 | gender=muʾannath | سابعة |
    | 8 | gender=muʾannath | ثامنة |
    | 9 | gender=muʾannath | تاسعة |

Scenario Outline: Masculine ordinal numbers from eleven through nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 13 |  | ثالث عشر |
    | 15 |  | خامس عشر |

Scenario Outline: Feminine ordinal numbers from eleven through nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 12 | gender=muʾannath | ثانية عشرة |
    | 13 | gender=muʾannath | ثالثة عشرة |
    | 15 | gender=muʾannath | خامسة عشرة |

Scenario: Ordinal conversion is supported
    Given I use the "AR" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "AR" number converter
    When I convert the fraction 3/2
    Then the result is "ثلاثة على اثنان"
