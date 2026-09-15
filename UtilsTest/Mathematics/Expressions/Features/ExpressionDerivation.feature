@Expressions @Derivation
Feature: Symbolic differentiation

Scenario Outline: Differentiate supported expressions
    Given the double parameters "x"
    And the source C-syntax expression "<source>"
    When I derive the expression with respect to "x"
    Then the transformed expression is numerically equivalent to "<expected>" at "<samples>" with tolerance <tolerance>

Examples:
    | source                       | expected                    | samples                    | tolerance |
    | 1                            | 0                           | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Exp(x)                       | Exp(x)                      | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | x                            | 1                           | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | x**2                         | 2*x                         | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | x**3                         | 3*x**2                      | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | x**3 + x**2 + x + 1         | 3*x**2 + 2*x + 1            | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Cos(x)                       | 0-Sin(x)                    | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Sin(x)                       | Cos(x)                      | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Sin(2*x)                     | 2*Cos(2*x)                  | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Sin(x) * Cos(x)              | Cos(x)**2-Sin(x)**2         | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Exp(x**2)                    | 2*x*Exp(x**2)               | -3.5,-1,-0.2,0.2,1,2.5    | 1e-9      |
    | Log(x)                       | 1/x                         | 0.5,1,2,2.718281828459045  | 1e-9      |
    | Log10(x)                     | 1/(x*Log(10))               | 0.5,1,2,10                 | 1e-9      |
    | Tan(x)                       | 1/(Cos(x)*Cos(x))           | 0,0.3,-0.5,1               | 1e-9      |
    | x/(x**2+1)                   | (1-x**2)/(x**2+1)**2        | -2,-1,0,1,2                | 1e-9      |
    | 5                            | 0                           | 42                         | 1e-9      |
