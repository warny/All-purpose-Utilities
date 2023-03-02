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
	When I compute matrix m3 = m1 + m2
	When I compute matrix m4 = m2 + m1
	Then I expect matrix m3 equals
	| C1 | C2 |
	| 6  | 8  |
	| 10 | 12 |
    And I expect m3 = m4
	And det(m1) = -2
	And det(m2) = -2
	And det(m3) = -8

Scenario: Matrix Multiplications
	Given m1 is a matrix
	| C1 | C2 |
	| 1  | 2  |
	| 3  | 4  |
	  And m2 is a matrix
	| C1 | C2 |
	| 5  | 6  |
	| 7  | 8  |
	When I compute matrix m = m1 * m2
	Then I expect matrix m equals
	| C1 | C2 |
	| 19 | 22 |
	| 43 | 50 |
	And det(m) = 4
	When I compute matrix m = m2 * m1
	Then I expect matrix m equals
	| C1 | C2 |
	| 23 | 34 |
	| 31 | 46 |
	And det(m) = 4

Scenario: Vector mulitplication
	Given m1 is a matrix
	| C1 | C2 |
	| 1  | 2  |
	| 3  | 4  |
	And Given these vectors
	| v1 | vr |
	| 5  | 19 |
	| 7  | 43 |
	When I compute vector vt = m1 * v1
	Then I expect vr = vt


Scenario: Matrix determinant 3*3
	Given m is a matrix
	| C1 | C2 | C3 |
	| 12 | 10 | 9  |
	| 2  | 8  | 6  |
	| 1  | 13 | 14 |
	Then det(m) = 350

Scenario: Matrix determinant 5*5
	Given m is a matrix
	| C1 | C2  | C3 | C4  | C5  |
	| 12 | 10  | 9  | -12 | 2   |
	| 2  | -8  | 6  | 4   | -4  |
	| 1  | -13 | 14 | 2   | 4   |
	| 1  | 8   | -6 | 7   | 4   |
	| -1 | -2  | 7  | 14  | -18 |
	Then det(m) = 294064