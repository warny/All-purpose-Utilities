# omy.Utils.Reflection (reflection helpers)

`omy.Utils.Reflection` adds convenience APIs over `System.Reflection` for accessing fields/properties and emitting delegates.

## Install
```bash
dotnet add package omy.Utils.Reflection
```

## Supported frameworks
- net8.0

## Features
- `PropertyOrFieldInfo` wrapper to read/write members without duplicating reflection logic.
- Dynamic delegate invocation helpers.
- Extensions to inspect member metadata consistently.

## Quick usage
```csharp
var info = new Utils.Reflection.PropertyOrFieldInfo(typeof(MyType).GetField("Id"));
int id = (int)info.GetValue(myObj);
info.SetValue(myObj, 42);
```

## Related packages
- `omy.Utils` – base utilities used by reflection helpers.
