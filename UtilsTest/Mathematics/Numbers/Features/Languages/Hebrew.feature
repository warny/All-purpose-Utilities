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
    | 1000 | אלף |

# NTS-11 — thousands. Sources: Unicode CLDR RBNF he (1000 אלף, 2000 אלפיים, 3000-10000 construct
# form + אלפים, 11000+ masculine number + אלף), ulpan.net ("שלושת אלפים" … "עשרת אלפים"; "we put
# the ve- before the last word": "אלף מאתיים שלושים וארבע", "אלפיים ותשע") and the Academy of the
# Hebrew Language ("אלפיים ועשרים", not "אלפיים עשרים"). "אלף" is a masculine noun, so the
# multiplier from eleven thousand takes the masculine form (CLDR, and the general rule that the
# number agrees with the counted noun).
Scenario Outline: Hebrew thousands
    Given I use the "HE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | אלף |
    | 2000 | אלפיים |
    | 3000 | שלושת אלפים |
    | 4000 | ארבעת אלפים |
    | 5000 | חמשת אלפים |
    | 6000 | ששת אלפים |
    | 7000 | שבעת אלפים |
    | 8000 | שמונת אלפים |
    | 9000 | תשעת אלפים |
    | 10000 | עשרת אלפים |
    | 11000 | אחד עשר אלף |
    | 12000 | שנים עשר אלף |
    | 13000 | שלושה עשר אלף |
    | 19000 | תשעה עשר אלף |
    | 20000 | עשרים אלף |
    | 21000 | עשרים ואחד אלף |
    | 22000 | עשרים ושניים אלף |
    | 23000 | עשרים ושלושה אלף |
    | 28000 | עשרים ושמונה אלף |
    | 100000 | מאה אלף |
    | 101000 | מאה ואחד אלף |
    | 102000 | מאה ושניים אלף |
    | 110000 | מאה ועשרה אלף |
    | 123000 | מאה עשרים ושלושה אלף |
    | 300000 | שלוש מאות אלף |

# The conjunction "ו" precedes only the last element of the whole number: it joins the lower
# group to the thousands when that group is a single element (a unit, a teen, a round ten or a
# round hundred), and is otherwise carried by the lower group's own last element. Sources: the
# Academy of the Hebrew Language ("ו' החיבור במספרים": a single ו before the last element) and
# m-math.co.il ("אלפיים ושמונה מאות", "שלושת אלפים ושבע מאות", "חמשת אלפים ושלוש מאות"). This
# deliberately diverges from CLDR RBNF he, which omits ו before a round ten or hundred
# ("אלף מאה"); see docs/NTS-08-linguistic-sources.md.
Scenario Outline: Hebrew thousands joined to the lower group
    Given I use the "HE" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | אלף ואחד |
    | 1002 | אלף ושתיים |
    | 1010 | אלף ועשר |
    | 1011 | אלף ואחת עשרה |
    | 1020 | אלף ועשרים |
    | 1021 | אלף עשרים ואחד |
    | 1100 | אלף ומאה |
    | 1101 | אלף מאה ואחד |
    | 1120 | אלף מאה ועשרים |
    | 1121 | אלף מאה עשרים ואחד |
    | 1234 | אלף מאתיים שלושים וארבע |
    | 2001 | אלפיים ואחד |
    | 2009 | אלפיים ותשע |
    | 2020 | אלפיים ועשרים |
    | 2800 | אלפיים ושמונה מאות |
    | 3700 | שלושת אלפים ושבע מאות |
    | 5300 | חמשת אלפים ושלוש מאות |
    | 1320 | אלף שלוש מאות ועשרים |
    | 3001 | שלושת אלפים ואחד |
    | 11001 | אחד עשר אלף ואחד |
    | 21021 | עשרים ואחד אלף עשרים ואחד |
    | 999999 | תשע מאות תשעים ותשעה אלף תשע מאות תשעים ותשע |

# The gender of the counted noun reaches the lower group only: the multiplier before אלף/אלפים
# agrees with the scale noun, whatever gender the caller selects.
Scenario Outline: Hebrew thousands with gender variants
    Given I use the "HE" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1000 | gender=zachar | אלף |
    | 2000 | gender=zachar | אלפיים |
    | 3000 | gender=zachar | שלושת אלפים |
    | 3000 | gender=nekeva | שלושת אלפים |
    | 11000 | gender=nekeva | אחד עשר אלף |
    | 21000 | gender=nekeva | עשרים ואחד אלף |
    | 21000 | gender=zachar | עשרים ואחד אלף |
    | 1001 | gender=nekeva | אלף ואחת |
    | 1003 | gender=zachar | אלף ושלושה |
    | 1003 | gender=nekeva | אלף ושלוש |
    | 1010 | gender=zachar | אלף ועשרה |
    | 1011 | gender=zachar | אלף ואחד עשר |
    | 1021 | gender=nekeva | אלף עשרים ואחת |
    | 3002 | gender=zachar | שלושת אלפים ושניים |

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
    | 110 |  | מאה ועשרה |
    | 1000 |  | אלף |
    | 1001 |  | אלף ואחד |
    | 1002 |  | אלף ושניים |
    | 2000 |  | אלפיים |
    | 3000 |  | שלושת אלפים |
    | 21000 |  | עשרים ואחד אלף |
    | 1001 | gender=nekeva | אלף ואחת |
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

# NTS-12: no ordinal of zero fits the library contract:
# Hebrew ordinal adjectives exist for 1-10 only, and zero is not used as an agreeing-cardinal
# ordinal.
# The converter fails closed instead of returning the cardinal "אפס" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "HE" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
