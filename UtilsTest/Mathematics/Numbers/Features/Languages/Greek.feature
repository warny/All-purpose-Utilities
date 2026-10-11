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

# NTS-08 validation: Λεξικό της Κοινής Νεοελληνικής (Triantafyllides): εικοστός "μετά το δέκατο
# ένατο", εκατοστός ("Η εκατοστή πρώτη μέρα"), διακοσιοστός, χιλιοστός. Both parts of a compound
# ordinal are ordinals agreeing in gender, so the feminine of 13 is δέκατη τρίτη.
Scenario Outline: Greek ordinals of the teens agree in both parts
    Given I use the "EL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 13 |  | δέκατος τρίτος |
    | 13 | gender=θηλυκό | δέκατη τρίτη |
    | 13 | gender=ουδέτερο | δέκατο τρίτο |
    | 19 | gender=θηλυκό | δέκατη ένατη |
    | 19 | gender=ουδέτερο | δέκατο ένατο |
    | 200 |  | διακοσιοστός |
    | 1000 |  | χιλιοστός |

# NTS-28: Λεξικό της Κοινής Νεοελληνικής: compounds juxtapose ordinals that all agree in gender (εικοστός:
# "Tην εικοστή τρίτη του μηνός"; εκατοστός: "Η εκατοστή πρώτη μέρα"), from the highest order down.
Scenario Outline: NTS-28 Greek compound ordinals agree in every part
    Given I use the "EL" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 21 |  | εικοστός πρώτος |
    | 21 | gender=θηλυκό | εικοστή πρώτη |
    | 23 | gender=θηλυκό | εικοστή τρίτη |
    | 23 | gender=ουδέτερο | εικοστό τρίτο |
    | 99 |  | ενενηκοστός ένατος |
    | 101 |  | εκατοστός πρώτος |
    | 101 | gender=θηλυκό | εκατοστή πρώτη |
    | 113 |  | εκατοστός δέκατος τρίτος |
    | 245 | gender=θηλυκό | διακοσιοστή τεσσαρακοστή πέμπτη |
    | 999 |  | εννιακοσιοστός ενενηκοστός ένατος |
    | 1001 |  | χιλιοστός πρώτος |
    | 1999 | gender=ουδέτερο | χιλιοστό εννιακοσιοστό ενενηκοστό ένατο |

# NTS-28: the round thousands above one (δισχιλιοστός, τρισχιλιοστός …) are not entries of the dictionary and
# no other consulted source establishes them: 2000 and above fail closed (deliberate limitation).
Scenario Outline: Greek ordinals outside the validated domain are not supported
    Given I use the "EL" number converter
    When I attempt to convert the ordinal number <number>
    Then conversion is rejected because no ordinal form is available

Examples:
    | number |
    | 2000 |
    | 2001 |
    | 10000 |

Scenario: Ordinal conversion is supported
    Given I use the "EL" number converter
    Then the converter supports ordinal conversion

Scenario: Fraction connector wording
    Given I use the "EL" number converter
    When I convert the fraction 3/2 through both public fraction APIs
    Then both fraction results are "τρία διά δύο"

Scenario Outline: Greek feminine cardinal numbers
    Given I use the "EL" number converter
    And I use the variants "gender=θηλυκό"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | μία |
    | 3 | τρεις |
    | 4 | τέσσερις |
    | 13 | δεκατρείς |
    | 14 | δεκατέσσερις |
    | 200 | διακόσιες |
    | 1000 | χίλιες |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "EL" number converter
    Then the converter supports clock-time conversion

# "η ώρα είναι μία / τρεις / τέσσερις": "ώρα" is feminine, so {hour} is forced to the feminine
# numeral. Quarter-hour 12-hour clock without day-part wording: "και τέταρτο", "και μισή",
# "<next> παρά τέταρτο".
Scenario Outline: Idiomatic Greek clock times
    Given I use the "EL" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | μία |
    | 01:15 | μία και τέταρτο |
    | 01:30 | μία και μισή |
    | 01:45 | δύο παρά τέταρτο |
    | 02:00 | δύο |
    | 03:00 | τρεις |
    | 04:00 | τέσσερις |
    | 12:45 | μία παρά τέταρτο |
    | 13:30 | μία και μισή |

# NTS-12: no ordinal of zero fits the library contract:
# "μηδενικός" is the adjective "zero" ("μηδενική ώρα"), not an ordinal numeral.
# The converter fails closed instead of returning the cardinal "μηδέν" unchanged.
Scenario: Zero has no ordinal form
    Given I use the "EL" number converter
    When I attempt to convert the ordinal number 0
    Then conversion is rejected because no ordinal form is available
