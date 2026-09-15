@Expressions @Simplification
Feature: Symbolic expression simplification

Scenario Outline: Simplify mathematical identities
    Given the double parameters "x"
    And the source C-syntax expression "<source>"
    When I simplify the expression
    Then the transformed expression is structurally equivalent to "<expected>"

Examples:
    | source                                | expected     |
    | Pow(Cos(x), 2.0) + Pow(Sin(x), 2.0)   | 1.0          |
    | Sin(x) / Cos(x)                       | Tan(x)       |
    | Cos(x) / Sin(x)                       | 1.0 / Tan(x) |
    | Cos(x) * Tan(x)                       | Sin(x)       |
    | Tan(x) * Cos(x)                       | Sin(x)       |
    | Sin(x) / Tan(x)                       | Cos(x)       |
    | 3.0*x + 2.0*x                         | 5.0*x        |
    | 3.0*x + x*2.0                         | 5.0*x        |
    | x + 2.0*x                             | 3.0*x        |
    | x - 2.0*x                             | -x           |
    | 2.0*x - 2.0*x                         | 0.0          |
    | -2.0*x + 2.0*x                        | 0.0          |

Scenario Outline: Put numeric expressions into canonical order
    Given the double parameters "<parameters>"
    And the source C-syntax expression "<source>"
    When I simplify the expression
    Then the transformed expression is structurally equivalent to "<expected>"

Examples:
    | parameters | source                                                        | expected           |
    | a,b        | b + a                                                         | a + b              |
    | A,a        | a + A                                                         | A + a              |
    | a,b,c,d    | (d + c) * (b + a)                                             | (a + b) * (c + d)  |
    | a,b        | b - a                                                         | -a + b             |
    | a,b        | a + b - a                                                     | b                  |
    | x,y        | Cos(y) + Cos(x)                                               | Cos(x) + Cos(y)    |
    | x          | Sin(x) + Cos(x)                                               | Cos(x) + Sin(x)    |
    | x,y        | Pow(Cos(x), 2.0) + Pow(Cos(y), 2.0) + Pow(Sin(x), 2.0) + Pow(Sin(y), 2.0) | 2.0       |
    | a,b,c      | (a + b) + c                                                   | a + (b + c)        |
    | a,b,c      | (c + b) - a                                                   | -a + (b + c)       |
    | a,b,c      | (c - b) - a                                                   | -a + (-b + c)      |
    | a,b,c      | (a / b) / c                                                   | a / (b * c)        |

Scenario Outline: Simplify one algebraic rule at a time
    Given the double parameters "<parameters>"
    And the source C-syntax expression "<source>"
    When I simplify the expression
    Then the transformed expression is structurally equivalent to "<expected>"

Examples:
    | parameters | source                    | expected          |
    | x          | x + 0.0                   | x                 |
    | x          | 0.0 + x                   | x                 |
    | x          | x - 0.0                   | x                 |
    | x          | 0.0 - x                   | -x                |
    | x          | x * 0.0                   | 0.0               |
    | x          | x * 1.0                   | x                 |
    | x          | 0.0 * x                   | 0.0               |
    | x          | 1.0 * x                   | x                 |
    | x          | x / 1.0                   | x                 |
    | x          | 0.0 / x                   | 0.0               |
    | x          | 0.0**x                    | 0.0               |
    | x          | 1.0**x                    | 1.0               |
    | x          | x**0.0                    | 1.0               |
    | x          | x**1.0                    | x                 |
    |             | 2.0 + 3.0                | 5.0               |
    |             | 5.0 - 3.0                | 2.0               |
    |             | 2.0 * 3.0                | 6.0               |
    |             | 6.0 / 3.0                | 2.0               |
    | x,y        | x + (-y)                  | x - y             |
    | x          | x + (-x)                  | 0.0               |
    | x,y        | (-x) + y                  | y - x             |
    | x          | (-x) + x                  | 0.0               |
    | x,y        | x - (-y)                  | x + y             |
    | x,y        | (-x) - y                  | -(x+y)            |
    | x,y        | -(x-y)                    | -(x+(0.0-y))      |
    | x,y,z      | x - (y+z)                 | (x-y)-z           |
    | x,y,z      | x - (y-z)                 | (x-y)-(0.0-z)     |
    | x          | 2.0*x + 3.0*x             | 5.0*x             |
    | x,y        | 2.0*x + 2.0*y             | (x+y)*2.0         |
    | x          | 3.0*x - 2.0*x             | x                 |
    | x,y        | 2.0*x - 2.0*y             | (x-y)*2.0         |
    | x          | x*2.0                     | 2.0*x             |
    | x          | 2.0*(3.0*x)               | 6.0*x             |
    | x,y        | (2.0*x)*(3.0*y)           | 6.0*(x*y)         |
    | x,y        | x*(-y)                    | -(x*y)            |
    | x,y        | (-x)*y                    | -(x*y)            |
    | x,y        | x/(-y)                    | -(x/y)            |
    | x,y        | (-x)/y                    | -(x/y)            |
    | x,y        | (2.0*x)*y                 | 2.0*(x*y)         |
    | x,y        | x*(2.0*y)                 | 2.0*(x*y)         |
    | x,y,z,w    | (x/y)/(z/w)               | (x*w)/(y*z)       |
    | x,y,z      | x/(y/z)                   | (x*z)/y           |
