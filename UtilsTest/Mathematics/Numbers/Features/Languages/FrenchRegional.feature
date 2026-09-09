@NumberToString @FR
Feature: FrenchRegional number conversion

Scenario Outline: Cardinal numbers below one thousand
    Given I use the "FR-fr" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -1 | moins un |
    | 0 | zéro |
    | 1 | un |
    | 2 | deux |
    | 11 | onze |
    | 20 | vingt |
    | 21 | vingt et un |
    | 22 | vingt deux |
    | 60 | soixante |
    | 61 | soixante et un |
    | 62 | soixante deux |
    | 111 | cent onze |
    | 121 | cent vingt et un |
    | 122 | cent vingt deux |
    | 160 | cent soixante |
    | 161 | cent soixante et un |
    | 162 | cent soixante deux |
    | 200 | deux cents |
    | 201 | deux cent un |
    | 221 | deux cent vingt et un |
    | 222 | deux cent vingt deux |
    | 260 | deux cent soixante |
    | 261 | deux cent soixante et un |
    | 262 | deux cent soixante deux |

Scenario Outline: Thousands
    Given I use the "FR-fr" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1000 | mille |
    | 1001 | mille un |
    | 1002 | mille deux |
    | 1011 | mille onze |
    | 1020 | mille vingt |
    | 1021 | mille vingt et un |
    | 1022 | mille vingt deux |
    | 1060 | mille soixante |
    | 1061 | mille soixante et un |
    | 1062 | mille soixante deux |
    | 1111 | mille cent onze |
    | 1121 | mille cent vingt et un |
    | 1122 | mille cent vingt deux |
    | 1160 | mille cent soixante |
    | 1161 | mille cent soixante et un |
    | 1162 | mille cent soixante deux |
    | 1200 | mille deux cents |
    | 1201 | mille deux cent un |
    | 1221 | mille deux cent vingt et un |
    | 1222 | mille deux cent vingt deux |
    | 1260 | mille deux cent soixante |
    | 1261 | mille deux cent soixante et un |
    | 1262 | mille deux cent soixante deux |

Scenario Outline: Tens of thousands
    Given I use the "FR-fr" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 12000 | douze mille |
    | 12001 | douze mille un |
    | 12002 | douze mille deux |
    | 12011 | douze mille onze |
    | 12020 | douze mille vingt |
    | 12021 | douze mille vingt et un |
    | 12022 | douze mille vingt deux |
    | 12060 | douze mille soixante |
    | 12061 | douze mille soixante et un |
    | 12062 | douze mille soixante deux |
    | 12111 | douze mille cent onze |
    | 12121 | douze mille cent vingt et un |
    | 12122 | douze mille cent vingt deux |
    | 99160 | quatre-vingt dix neuf mille cent soixante |
    | 99161 | quatre-vingt dix neuf mille cent soixante et un |
    | 99162 | quatre-vingt dix neuf mille cent soixante deux |
    | 99200 | quatre-vingt dix neuf mille deux cents |
    | 99201 | quatre-vingt dix neuf mille deux cent un |
    | 99221 | quatre-vingt dix neuf mille deux cent vingt et un |
    | 99222 | quatre-vingt dix neuf mille deux cent vingt deux |
    | 99260 | quatre-vingt dix neuf mille deux cent soixante |
    | 99261 | quatre-vingt dix neuf mille deux cent soixante et un |
    | 99262 | quatre-vingt dix neuf mille deux cent soixante deux |

Scenario Outline: Large cardinal numbers
    Given I use the "FR-fr" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | -401000 | moins quatre cent un mille |
    | 401000 | quatre cent un mille |
    | 999999 | neuf cent quatre-vingt dix neuf mille neuf cent quatre-vingt dix neuf |
    | 1000000 | un million |
    | 999999999 | neuf cent quatre-vingt dix neuf millions neuf cent quatre-vingt dix neuf mille neuf cent quatre-vingt dix neuf |

Scenario Outline: Decimal numbers
    Given I use the "FR-fr" number converter
    When I convert the decimal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1.5 | un virgule cinq dixièmes |
    | 12.34 | douze virgule trente quatre centièmes |

Scenario Outline: French ordinal numbers
    Given I use the "FR" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | premier |
    | 2 | deuxième |
    | 3 | troisième |
    | 4 | quatrième |
    | 5 | cinquième |
    | 6 | sixième |
    | 8 | huitième |
    | 9 | neuvième |
    | 10 | dixième |
    | 11 | onzième |
    | 20 | vingtième |
    | 21 | vingt et unième |
    | 100 | centième |
    | 1000 | millième |

Scenario Outline: French feminine cardinal numbers
    Given I use the "FR" number converter
    And I use the variants "gender=feminin"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | une |
    | 21 | vingt et une |
    | 31 | trente et une |
    | 61 | soixante et une |
    | 1000000 | un million |
    | 1000021 | un million vingt et une |

Scenario Outline: Belgian French feminine cardinal numbers
    Given I use the "FR-be" number converter
    And I use the variants "gender=feminin"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | une |
    | 21 | vingt et une |
    | 71 | septante et une |
    | 81 | huitante et une |
    | 91 | nonante et une |
    | 1000000 | un million |

Scenario Outline: Belgian French ordinal numbers
    Given I use the "FR-be" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | premier |
    | 2 | deuxième |
    | 5 | cinquième |
    | 9 | neuvième |
    | 21 | vingt et unième |
    | 70 | septantième |
    | 71 | septante et unième |
    | 80 | huitantième |
    | 81 | huitante et unième |
    | 90 | nonantième |
    | 91 | nonante et unième |

Scenario Outline: Explicit masculine cardinal numbers
    Given I use the "<culture>" number converter
    And I use the variants "gender=masculin"
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | culture | number | expected |
    | FR | 1 | un |
    | FR | 21 | vingt et un |
    | FR-be | 1 | un |
    | FR-be | 71 | septante et un |

Scenario Outline: Duration wording
    Given I use the "FR" number converter
    When I convert the duration "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 01:00:00 | une heure |
    | 21:00:00 | vingt et une heures |
    | 00:01:00 | une minute |
    | 00:21:00 | vingt et une minutes |
    | 00:00:01 | une seconde |
    | 00:00:21 | vingt et une secondes |
    | 01:21:21 | une heure vingt et une minutes vingt et une secondes |
    | 02:30:00 | deux heures trente minutes |

Scenario Outline: Time-of-day wording
    Given I use the "FR" number converter
    When I convert the time "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 14:30:00 | quatorze heures trente minutes |
    | 01:21:21 | une heure vingt et une minutes vingt et une secondes |

Scenario Outline: Date wording
    Given I use the "FR" number converter
    When I convert the date "<value>"
    Then the result is "<expected>"

Examples:
    | value | expected |
    | 2026-07-01 | premier juillet deux mille vingt six |
    | 2026-07-02 | deux juillet deux mille vingt six |

Scenario: Date and time wording
    Given I use the "FR" number converter
    When I convert the date and time "2026-07-02T14:30:05"
    Then the result is "deux juillet deux mille vingt six quatorze heures trente minutes cinq secondes"

Scenario Outline: Feminine first ordinal
    Given I use the "FR" number converter
    And I use the variants "<variants>"
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | variants | expected |
    | 1 | gender=feminin | première |
