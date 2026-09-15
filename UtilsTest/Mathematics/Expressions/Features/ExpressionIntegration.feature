@Expressions @Integration
Feature: Symbolic integration

Scenario Outline: Integrate supported expressions into a canonical structure
    Given the double parameters "x"
    And the source C-syntax expression "<source>"
    When I integrate and simplify the expression with respect to "x"
    Then the normalized transformed expression is structurally equivalent to "<expected>"

Examples:
    | source      | expected       |
    | 1/x         | Log(Abs(x))    |
    | 1/(x**2)    | -(1.0/x)       |
    | 1/Sqrt(x)   | 2.0*Sqrt(x)    |
    | Sinh(x)     | Cosh(x)        |
    | Cosh(x)     | Sinh(x)        |
    | Tanh(x)     | Log(Cosh(x))   |

Scenario Outline: Integrate ordinary mathematical formulas
    Given the double parameters "x"
    And the source C-syntax expression "<source>"
    When I integrate the expression with respect to "x"
    Then the transformed expression is numerically equivalent to "<expected>" at "<samples>" with tolerance <tolerance>

Examples:
    | source       | expected                         | samples                                   | tolerance |
    | 1.0          | x                                | -2,-1,0,1,2.5                             | 1e-9      |
    | x            | x**2.0/2.0                       | -2,-1,0,1,2.5                             | 1e-9      |
    | x**2         | x**3.0/3.0                       | -2,-1,0,1,2                               | 1e-9      |
    | 3.0/x        | 3.0*Log(x)                       | 0.5,1,2.718281828459045,5                 | 1e-9      |
    | Sin(x)       | -Cos(x)                          | -3.141592653589793,-1.5707963267948966,0,1.5707963267948966,3.141592653589793 | 1e-9 |
    | Cos(x)       | Sin(x)                           | -3.141592653589793,-1.5707963267948966,0,1.5707963267948966,3.141592653589793 | 1e-9 |
    | Sin(2.0*x)   | -Cos(2.0*x)/2.0                  | -1.5707963267948966,0,0.7853981633974483,3.141592653589793 | 1e-9 |
    | Exp(x)       | Exp(x)                           | -2,-1,0,1,2                               | 1e-9      |
    | Log(x)       | x*(Log(x)-1.0)                   | 0.5,1,2.718281828459045,5                 | 1e-9      |
    | Log10(x)     | x*Log10(x)-x/Log(10.0)           | 1,2,10,100                                | 1e-9      |
    | 2.0*Sin(x)   | -2.0*Cos(x)                      | -3.141592653589793,0,1.5707963267948966,3.141592653589793 | 1e-9 |
    | x + Sin(x)   | x**2.0/2.0-Cos(x)                | -1.5707963267948966,0,1,3.141592653589793 | 1e-9      |
    | -x           | -(x**2.0/2.0)                    | -2,-1,0,1,2                               | 1e-9      |
    | x + 1.0      | x**2.0/2.0+x                    | -2,-1,0,1,2                               | 1e-9      |
    | x - 1.0      | x**2.0/2.0-x                    | -2,-1,0,1,2                               | 1e-9      |
    | 2.0*x        | x**2.0                           | -2,-1,0,1,2                               | 1e-9      |
    | x*2.0        | x**2.0                           | -2,-1,0,1,2                               | 1e-9      |
    | x/2.0        | x**2.0/4.0                       | -2,-1,0,1,2                               | 1e-9      |
    | 3.0/x        | 3.0*Log(Abs(x))                  | -5,-2,-0.5,0.5,2,5                        | 1e-9      |
    | 1.0/Sqrt(x)  | 2.0*Sqrt(x)                     | 0.5,1,2,5                                 | 1e-9      |
    | 1.0/(x**1.0) | Log(Abs(x))                     | 0.5,1,2,5                                 | 1e-9      |
    | 1.0/(x**2.0) | -(1.0/x)                        | 0.5,1,2,5                                 | 1e-9      |
    | 1/(x**1)     | Log(Abs(x))                     | 0.5,1,2,5                                 | 1e-9      |
    | x**2.0       | x**3.0/3.0                      | -2,-1,0,1,2                               | 1e-9      |
    | Exp(2.0*x)   | Exp(2.0*x)/2.0                  | -2,-1,0,1,2                               | 1e-9      |
    | Cos(2.0*x)   | Sin(2.0*x)/2.0                  | -1,-0.5,0,0.5,1                           | 1e-9      |
    | Tan(x)       | -Log(Abs(Cos(x)))                | -1,-0.5,0,0.5,1                           | 1e-9      |
    | Tan(2.0*x)   | -Log(Abs(Cos(2.0*x)))/2.0       | -0.5,-0.2,0,0.2,0.5                      | 1e-9      |
    | Sinh(2.0*x)  | Cosh(2.0*x)/2.0                 | -2,-1,0,1,2                               | 1e-9      |
    | Cosh(2.0*x)  | Sinh(2.0*x)/2.0                 | -2,-1,0,1,2                               | 1e-9      |
    | Tanh(2.0*x)  | Log(Cosh(2.0*x))/2.0            | -2,-1,0,1,2                               | 1e-9      |

Scenario: Integrate a non-target parameter
    Given the double parameters "x,y"
    And the source C-syntax expression "y"
    When I integrate the expression with respect to "x"
    Then the transformed expression is structurally equivalent to "y*x"
