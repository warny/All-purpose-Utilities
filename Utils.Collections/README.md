# omy.Utils.Collections

`omy.Utils.Collections` packages the skip list implementation from the main utilities set into a dedicated library focused on ordered collections.

## Installation

```bash
dotnet add package omy.Utils.Collections
```

## Usage

```csharp
using Utils.Collections;

// Create a skip list with a custom threshold for balancing upper levels.
var numbers = new SkipList<int>(threshold: 5);

numbers.Add(3);
numbers.Add(1);
numbers.Add(2);

// Elements are kept sorted during insertion.
foreach (var number in numbers)
{
    Console.WriteLine(number);
}
```

The `SkipList<T>` type provides `Add`, `Remove`, `Contains`, and enumeration capabilities while maintaining sorted order with logarithmic-time expectations.
