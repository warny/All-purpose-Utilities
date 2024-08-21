Feature: DoubleIndexedDictionaryTests

Scenario: Add Distinct Values 1 
	Given An empty DoubleIndexedDictionary
	When I add (1, "A")
	 And I add (2, "B")
	 And I add (3, "C")
	Then I except that key value content
		| Key | Value |
		| 1   | A     |
		| 2   | B     |
		| 3   | C     |

Scenario: Add Distinct Values 2 
	Given An empty DoubleIndexedDictionary
	When I add ("A", 1)
	 And I add ("B", 2)
	 And I add ("C", 3)
	Then I except that key value content
		| Key | Value |
		| 1   | A     |
		| 2   | B     |
		| 3   | C     |

Scenario: Add Distinct Values 3 
	Given An empty DoubleIndexedDictionary
	When I add ("A", 1)
	 And I add (2, "B")
	 And I add ("C", 3)
	Then I except that key value content
		| Key | Value |
		| 1   | A     |
		| 2   | B     |
		| 3   | C     |

Scenario: Add Same Values 1 
	Given An empty DoubleIndexedDictionary
	When I add (1, "A")
	 And I add (2, "A")
	Then I expect an exception ArgumentException

Scenario: Add Same Values 2 
	Given An empty DoubleIndexedDictionary
	When I add (1, "A")
	 And I add (1, "B")
	Then I expect an exception ArgumentException

Scenario: Read Value 1
	Given A prefilled DoubleIndexedDictionary
	Then I expect [1] = "A"
	 And I expect ["A"] = 1
	 And I expect [999] throws KeyNotFoundException 
	 And I expect ["A"] throws KeyNotFoundException 
