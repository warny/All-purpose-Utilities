@Expressions @Gradient
Feature: Symbolic gradients

Scenario: Calculate a single-variable gradient
    Given the double parameters "x"
    And the source C-syntax expression "x*x"
    When I calculate the gradient
    Then the gradient is structurally equivalent to
        | variable | expression |
        | x        | 2.0*x      |

Scenario: Calculate both partial derivatives
    Given the double parameters "x,y"
    And the source C-syntax expression "x*y"
    When I calculate the gradient
    Then the gradient is structurally equivalent to
        | variable | expression |
        | x        | y          |
        | y        | x          |

Scenario: Calculate an explicit subset of a gradient
    Given the double parameters "x,y"
    And the source C-syntax expression "x*x + y"
    When I calculate the gradient with respect to "x"
    Then the gradient is structurally equivalent to
        | variable | expression |
        | x        | 2.0*x      |

Scenario: Calculate the gradient of a constant
    Given the double parameters "x"
    And the source C-syntax expression "5.0"
    When I calculate the gradient
    Then the gradient is structurally equivalent to
        | variable | expression |
        | x        | 0.0        |
