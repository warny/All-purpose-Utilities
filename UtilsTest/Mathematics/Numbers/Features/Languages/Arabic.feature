@NumberToString @AR
Feature: Arabic number conversion

Scenario Outline: Decimal numbers
    Given I use the "AR" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | واحد فاصل خمسة |
    | 12.34 | اثنا عشر فاصل ثلاثة أربعة |

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
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "ثلاثة على اثنان"

# Compound cardinals: 11-19 put the unit first and keep "عشر" ("أحد عشر", "اثنا عشر"; feminine
# "إحدى عشرة", "اثنتا عشرة"); 21-99 put the unit first joined by "و" ("واحد وعشرون"); hundreds
# join their remainder with "و" ("مائة وواحد").
Scenario Outline: Arabic compound cardinal numbers
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 11 |  | أحد عشر |
    | 12 |  | اثنا عشر |
    | 13 |  | ثلاثة عشر |
    | 19 |  | تسعة عشر |
    | 21 |  | واحد وعشرون |
    | 22 |  | اثنان وعشرون |
    | 35 |  | خمسة وثلاثون |
    | 101 |  | مائة وواحد |
    | 121 |  | مائة وواحد وعشرون |
    | 11 | gender=muʾannath | إحدى عشرة |
    | 12 | gender=muʾannath | اثنتا عشرة |
    | 13 | gender=muʾannath | ثلاث عشرة |
    | 21 | gender=muʾannath | واحدة وعشرون |
    | 23 | gender=muʾannath | ثلاث وعشرون |

# Contract: like 1-19, ordinals are the indefinite short nominative form without the article.
# From 21 the unit is an ordinal and the tens stay cardinal ("حادٍ وعشرون", feminine "حادية
# وعشرون"); round tens, one hundred and one thousand use the cardinal word, which is the
# ordinal form in Modern Standard Arabic ("العشرون", "المائة", "الألف" with the article).
Scenario Outline: Arabic ordinals above nineteen
    Given I use the "AR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 20 |  | عشرون |
    | 21 |  | حادٍ وعشرون |
    | 22 |  | ثانٍ وعشرون |
    | 23 |  | ثالث وعشرون |
    | 30 |  | ثلاثون |
    | 99 |  | تاسع وتسعون |
    | 100 |  | مائة |
    | 1000 |  | ألف |
    | 20 | gender=muʾannath | عشرون |
    | 21 | gender=muʾannath | حادية وعشرون |
    | 22 | gender=muʾannath | ثانية وعشرون |
    | 23 | gender=muʾannath | ثالثة وعشرون |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "AR" number converter
    Then the converter supports clock-time conversion

# "الساعة" is feminine: the hour is the definite feminine ordinal ("الثانية", "الحادية عشرة"),
# except one, which is "الواحدة". Quarter-hour 12-hour clock without day-part wording; the
# only vowel marks are the tanwin already used by the configuration ("ثانٍ", "ربعًا").
Scenario Outline: Idiomatic Arabic clock times
    Given I use the "AR" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | الواحدة |
    | 01:15 | الواحدة والربع |
    | 01:30 | الواحدة والنصف |
    | 01:45 | الثانية إلا ربعًا |
    | 02:00 | الثانية |
    | 03:00 | الثالثة |
    | 11:00 | الحادية عشرة |
    | 12:00 | الثانية عشرة |
    | 12:45 | الواحدة إلا ربعًا |
    | 13:30 | الواحدة والنصف |
