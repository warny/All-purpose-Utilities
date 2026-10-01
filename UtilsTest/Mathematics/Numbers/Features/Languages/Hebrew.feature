@NumberToString @HE
Feature: Hebrew number conversion

Scenario Outline: Decimal numbers
    Given I use the "HE" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | אחד נקודה חמש |
    | 12.34 | שתים עשרה נקודה שלוש ארבע |

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
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "שלוש על שתיים"

# Compound cardinals (Academy of the Hebrew Language): 11-19 keep the gender of the counted noun
# ("אחד עשר", "שנים עשר" masculine; "אחת עשרה", "שתים עשרה" feminine and counting); the last
# element of a compound is joined by "ו" ("עשרים ואחד", "מאה ואחד", "מאה עשרים ואחד").
Scenario Outline: Hebrew compound cardinal numbers
    Given I use the "HE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | אחת עשרה |
    | 12 |  | שתים עשרה |
    | 13 |  | שלוש עשרה |
    | 21 |  | עשרים ואחד |
    | 22 |  | עשרים ושתיים |
    | 101 |  | מאה ואחד |
    | 110 |  | מאה ועשר |
    | 111 |  | מאה ואחת עשרה |
    | 120 |  | מאה ועשרים |
    | 121 |  | מאה עשרים ואחד |
    | 11 | gender=zachar | אחד עשר |
    | 12 | gender=zachar | שנים עשר |
    | 13 | gender=zachar | שלושה עשר |
    | 22 | gender=zachar | עשרים ושניים |
    | 23 | gender=zachar | עשרים ושלושה |
    | 21 | gender=nekeva | עשרים ואחת |
    | 12 | gender=nekeva | שתים עשרה |

# Contract: Hebrew has dedicated ordinal adjectives only for 1-10. Above ten the ordinal is the
# cardinal agreeing with the noun ("הפרק האחד עשר", "השנה העשרים ואחת"); this cardinal form is
# intentional, not an accidental fallback. Without a gender the masculine form is used, like 1-10.
Scenario Outline: Hebrew ordinals above ten use the agreeing cardinal
    Given I use the "HE" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | אחד עשר |
    | 12 |  | שנים עשר |
    | 20 |  | עשרים |
    | 21 |  | עשרים ואחד |
    | 23 |  | עשרים ושלושה |
    | 100 |  | מאה |
    | 11 | gender=nekeva | אחת עשרה |
    | 12 | gender=nekeva | שתים עשרה |
    | 21 | gender=nekeva | עשרים ואחת |
    | 12 | gender=zachar | שנים עשר |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "HE" number converter
    Then the converter supports clock-time conversion

# "השעה אחת": "שעה" is feminine, so {hour} is the feminine cardinal; "שתיים" follows the
# spelling already used by the configuration. Quarter-hour 12-hour clock without day part.
Scenario Outline: Idiomatic Hebrew clock times
    Given I use the "HE" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | אחת |
    | 01:15 | אחת ורבע |
    | 01:30 | אחת וחצי |
    | 01:45 | רבע לשתיים |
    | 02:00 | שתיים |
    | 12:00 | שתים עשרה |
    | 12:45 | רבע לאחת |
    | 13:30 | אחת וחצי |
