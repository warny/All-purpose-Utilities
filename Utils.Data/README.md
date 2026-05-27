# omy.Utils.Data (data utilities)

`omy.Utils.Data` maps `IDataRecord`/`IDataReader` rows to typed objects, builds safe parameterized SQL through C# string interpolation, and parses/reformats SQL statements via an AST.

## Install
```bash
dotnet add package omy.Utils.Data
```

## Supported frameworks
- net8.0

## Features
- `[Field]` attribute — maps properties/fields to data-record columns by name or index.
- `DataUtils.ToObject<T>` / `FillObject` / `AsEnumerable<T>` — row-to-object mapping.
- `DataUtils.GetDbType` / `ToDbValue` — .NET type ↔ `DbType` conversion helpers.
- `SqlBuilderInterpolator` — C# interpolated strings become parameterized SQL (zero-injection SQL builder).
- `DbConnectionExtensions.CreateCommand` — one-call parameterized command from a connection.
- `DbCommandExtensions.SetCommandText` / `AddNewParameter` — attach interpolated SQL to an existing command.
- `SqlCommandFactory` — reusable factory that centralizes `SqlSyntaxOptions`.
- `SqlQueryAnalyzer.Parse` — parses SELECT/INSERT/UPDATE/DELETE into a mutable `SqlQuery` AST.
- `SqlFormattingOptions` — inline, prefixed, or suffixed pretty-printing for rebuilt SQL.

## Quick usage

```csharp
using System.Data;
using Utils.Data;

public class User
{
    [Field("id")]           public int    Id   { get; set; }
    [Field("display_name")] public string Name { get; set; }
}

// Map a single record
User user = record.ToObject<User>();

// Stream all rows
IEnumerable<User> users = reader.AsEnumerable<User>();
```

## Field mapping examples

```csharp
using Utils.Data;

public class Product
{
    // Map by column name
    [Field("product_id")]   public int    Id       { get; set; }
    [Field("product_name")] public string Name     { get; set; }

    // Map by ordinal index
    [Field(2)]              public decimal Price   { get; set; }

    // Map by name AND confirm index (useful for validation)
    [Field("stock", 3)]     public int    Stock    { get; set; }
}

// Fill an existing object in-place
record.FillObject(existingProduct);
```

## Parameterized SQL (interpolated string builder)

`DbConnectionExtensions.CreateCommand` turns a C# interpolated string into a fully parameterized `IDbCommand`. Each interpolated hole becomes a named parameter — no string-concatenation, no injection risk.

```csharp
using System.Data;
using Utils.Data;

IDbConnection connection = /* your connection */;

int    userId = 42;
string role   = "admin";

// Each {hole} becomes a parameter — userId → @userId, role → @role
IDbCommand cmd = connection.CreateCommand(
    $"SELECT * FROM users WHERE id = {userId} AND role = {role}");

// Produces: SELECT * FROM users WHERE id = @userId AND role = @role
// with parameters @userId = 42, @role = "admin"
```

### Attach interpolated SQL to an existing command

```csharp
using System.Data;
using Utils.Data;

IDbCommand cmd = connection.CreateCommand();
cmd.SetCommandText($"DELETE FROM logs WHERE created_at < {cutoffDate}");
```

### Add parameters manually

```csharp
using System.Data;
using Utils.Data;

IDbCommand cmd = connection.CreateCommand();

// Auto-infer DbType from value
cmd.AddNewParameter("userId", 42);

// Explicit DbType
cmd.AddNewParameter("status", DbType.String, "active");

// Return the parameter for further configuration
IDbDataParameter p = cmd.CreateParameter("amount", DbType.Decimal, 9.99m);
p.Precision = 10;
p.Scale = 2;
cmd.Parameters.Add(p);
```

### SqlCommandFactory (reusable syntax options)

```csharp
using System.Data;
using System.Data.Common;
using Utils.Data;
using Utils.Data.Sql;

// MySQL-style parameters use '?'; use ':' prefix for PostgreSQL, etc.
var factory = new SqlCommandFactory(new SqlSyntaxOptions(autoParameterPrefix: ':'));

DbConnection connection = /* your connection */;
int minAge = 18;

DbCommand cmd = factory.CreateCommand(connection,
    $"SELECT * FROM members WHERE age >= {minAge}");
// → SELECT * FROM members WHERE age >= :minAge  (parameter :minAge = 18)
```

## DbType inference

`DataUtils.GetDbType` maps CLR types to `DbType` automatically:

```csharp
using System.Data;
using Utils.Data;

DbType t1 = DataUtils.GetDbType(42);          // DbType.Int32
DbType t2 = DataUtils.GetDbType("hello");     // DbType.String
DbType t3 = DataUtils.GetDbType(DateTime.Now);// DbType.DateTime
DbType t4 = DataUtils.GetDbType<int?>(null);  // DbType.Int32 (unwraps Nullable<T>)

// Convert null to DBNull for command parameters
object dbVal = ((object?)null).ToDbValue();   // DBNull.Value
object dbVal2 = "hello".ToDbValue();          // "hello"
```

## SQL query analysis and pretty-printing

`SqlQueryAnalyzer.Parse` turns a SQL string into a mutable AST that you can inspect or reformat.

```csharp
using Utils.Data.Sql;

string sql = "SELECT id, name FROM users WHERE active = 1 ORDER BY name";
SqlQuery query = SqlQueryAnalyzer.Parse(sql);

// Inspect the parsed structure
var select = (SqlSelectStatement)query.RootStatement;
Console.WriteLine(select.SelectPart.ToSql());  // id, name
Console.WriteLine(select.WherePart?.ToSql());  // active = 1

// Rebuild SQL inline (default)
string inline = query.ToSql();

// Pretty-print with line breaks (suffixed commas)
var opts = new SqlFormattingOptions(SqlFormattingMode.Suffixed, indentSize: 2);
string pretty = query.ToSql(opts);
```

### Modify a parsed statement

```csharp
using Utils.Data.Sql;

SqlQuery query = SqlQueryAnalyzer.Parse(
    "SELECT id, email FROM users");

var select = (SqlSelectStatement)query.RootStatement;

// Ensure a WHERE clause exists, then add a condition
select.EnsureWhereSegment().AddRaw("active = 1");

// Add a column to SELECT
select.SelectPart.Segment.AddCommaSeparatedElement("created_at");

Console.WriteLine(query.ToSql());
// → SELECT id, email, created_at FROM users WHERE active = 1
```

### Formatting modes

```csharp
using Utils.Data.Sql;

SqlQuery q = SqlQueryAnalyzer.Parse(
    "SELECT a, b, c FROM t WHERE x = 1 ORDER BY a");

// Inline (default): SELECT a, b, c FROM t WHERE x = 1 ORDER BY a
Console.WriteLine(q.ToSql(new SqlFormattingOptions(SqlFormattingMode.Inline)));

// Prefixed — commas lead each line
Console.WriteLine(q.ToSql(new SqlFormattingOptions(SqlFormattingMode.Prefixed)));

// Suffixed — commas trail each line
Console.WriteLine(q.ToSql(new SqlFormattingOptions(SqlFormattingMode.Suffixed)));
```

### CTE queries

```csharp
using Utils.Data.Sql;

string sql = @"
WITH recent AS (
    SELECT id, name FROM orders WHERE created_at > '2024-01-01'
)
SELECT * FROM recent WHERE total > 100";

SqlQuery query = SqlQueryAnalyzer.Parse(sql);
var select = (SqlSelectStatement)query.RootStatement;

// Inspect CTE definitions
foreach (var cte in select.WithClause!.Definitions)
{
    Console.WriteLine($"CTE: {cte.Name}");
}
```

## Related packages
- `omy.Utils` – shared helpers the data utilities rely on.
- `omy.Utils.IO` – for binary/stream serialization scenarios.
