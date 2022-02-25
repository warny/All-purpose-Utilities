Feature: MatrixTests

A short summary of the feature

Scenario: Matrix Additions
	Given m1 is a matrix
	| C1 | C2 |
	| 1  | 2  |
	| 3  | 4  |
	  And m2 is a matrix
	| C1 | C2 |
	| 5  | 6  |
	| 7  | 8  |
	When I compute m3 = m1 + m2
	When I compute m4 = m2 + m1
	Then I expect matrix m3 equals
	| C1 | C2 |
	| 6  | 8  |
	| 10 | 12 |
    And I expect m3 equals m4

Scenario: Matrix Multiplications
	Given m1 is a matrix
	| C1 | C2 |
	| 1  | 2  |
	| 3  | 4  |
	  And m2 is a matrix
	| C1 | C2 |
	| 5  | 6  |
	| 7  | 8  |
	When I compute m = m1 * m2
	Then I expect matrix m equals
	| C1 | C2 |
	| 19 | 22 |
	| 43 | 50 |
	When I compute m = m2 * m1
	Then I expect matrix m equals
	| C1 | C2 |
	| 23 | 34 |
	| 31 | 46 |

