@Expressions @Simplification
Feature: Symbolic expression simplification

Scenario Outline: Simplify mathematical identities
    Given the double parameters "x"
    And the source C-syntax expression "<source>"
    When I simplify the expression
    Then the transformed expression is structurally equivalent to "<expected>"

Examples:
    | source                                | expected     |
    | Pow(Cos(x), 2) + Pow(Sin(x), 2)       | 1            |
    | Sin(x) / Cos(x)                       | Tan(x)       |
    | Cos(x) / Sin(x)                       | 1 / Tan(x)   |
    | Cos(x) * Tan(x)                       | Sin(x)       |
    | Tan(x) * Cos(x)                       | Sin(x)       |
    | Sin(x) / Tan(x)                       | Cos(x)       |
    | 3*x + 2*x                             | 5*x          |
    | 3*x + x*2                             | 5*x          |
    | x + 2*x                               | 3*x          |
    | x - 2*x                               | -x           |
    | 2*x - 2*x                             | 0            |
    | -2*x + 2*x                            | 0            |

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
    | x,y        | Pow(Cos(x), 2) + Pow(Cos(y), 2) + Pow(Sin(x), 2) + Pow(Sin(y), 2) | 2               |
    | a,b,c      | (a + b) + c                                                   | a + (b + c)        |
    | a,b,c      | (c + b) - a                                                   | -a + (b + c)       |
    | a,b,c      | (c - b) - a                                                   | -a + (-b + c)      |
    | a,b,c      | (a / b) / c                                                   | a / (b * c)        |
