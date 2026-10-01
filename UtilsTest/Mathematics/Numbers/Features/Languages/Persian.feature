@NumberToString @FA
Feature: Persian number conversion

Background:
    Given I use the "FA" number converter

Scenario Outline: Cardinal numbers
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 0 | صفر |
    | 1 | یک |
    | 2 | دو |
    | 3 | سه |
    | 9 | نه |
    | 10 | ده |
    | 11 | یازده |
    | 12 | دوازده |
    | 19 | نوزده |
    | 20 | بیست |
    | 21 | بیست و یک |
    | 30 | سی |
    | 100 | صد |
    | 101 | صد و یک |
    | 200 | دویست |
    | 1000 | هزار |
    | 2000 | دو هزار |
    | 10000 | ده هزار |
    | 1000000 | یک میلیون |
    | 1000000000 | یک میلیارد |
    | 1000000000000 | یک تریلیون |
    | -1 | منفی یک |
    | -10 | منفی ده |

Scenario: The regional alias uses Persian wording
    Then the "FA" and "FA-IR" converters produce the same cardinal wording for 2

Scenario: Values above the supported maximum are rejected
    When I attempt to convert the cardinal number 1000000000000000
    Then conversion is rejected because the value is out of range

Scenario: Ordinal conversion is supported
    Given I use the "FA" number converter
    Then the converter supports ordinal conversion

# Persian ordinals add "م" to the cardinal ("دوم", "بیست و یکم"); "سه" gives "سوم" and a final
# vowel takes "ام" after a zero-width non-joiner ("سی‌ام"). The first is "اول" when standalone
# (the Academy also accepts "یکم"); compounds keep the productive "یکم" ("بیست و یکم").
# Groups are joined by "و" ("هزار و یک").
Scenario Outline: Persian ordinal numbers
    Given I use the "FA" number converter
    When I convert the ordinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1 | اول |
    | 2 | دوم |
    | 3 | سوم |
    | 4 | چهارم |
    | 5 | پنجم |
    | 10 | دهم |
    | 11 | یازدهم |
    | 20 | بیستم |
    | 21 | بیست و یکم |
    | 23 | بیست و سوم |
    | 30 | سی‌ام |
    | 100 | صدم |
    | 101 | صد و یکم |
    | 1000 | هزارم |
    | 1001 | هزار و یکم |

Scenario Outline: Persian cardinals join groups with "و"
    Given I use the "FA" number converter
    When I convert the cardinal number <number>
    Then the result is "<expected>"

Examples:
    | number | expected |
    | 1001 | هزار و یک |
    | 2021 | دو هزار و بیست و یک |

Scenario: Idiomatic clock-time conversion is supported
    Given I use the "FA" number converter
    Then the converter supports clock-time conversion

# "ساعت یک", "یک و ربع", "یک و نیم", "یک ربع به دو". Quarter-hour 12-hour clock without
# day-part wording; the quarter to the hour refers to the following hour.
Scenario Outline: Idiomatic Persian clock times
    Given I use the "FA" number converter
    When I convert the clock time "<time>"
    Then the result is "<expected>"

Examples:
    | time | expected |
    | 01:00 | ساعت یک |
    | 01:15 | یک و ربع |
    | 01:30 | یک و نیم |
    | 01:45 | یک ربع به دو |
    | 02:00 | ساعت دو |
    | 12:45 | یک ربع به یک |
    | 13:30 | یک و نیم |
