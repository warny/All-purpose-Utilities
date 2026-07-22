# Utils.OData — Quality, protocol, and API audit (2026-07-11)

Initial review of the `Utils.OData` package, focused on URI/query construction, HTTP forwarding, pagination, response lifetime, LINQ translation, metadata loading, error contracts, concurrency, tests, and documentation. No production code is changed by this commit.

## Critical and high-priority findings

### 1. **[FIXED]** Boolean query parameters are always emitted, including `$count=false`

`ODataQueryBuilder.AddQueryString(..., bool value, bool? defaultValue)` removes the parameter when it equals the default, but then immediately assigns it again unconditionally:

```csharp
if (value == defaultValue) queryString.Remove(name);
queryString[name] = value.ToString();
```

The constructor calls this method for `$count` with `defaultValue: false`, so `$count=false` is still serialized for every query whose `Count` property is false.

**Risk:** generated URIs do not follow the intended omission policy, cache keys differ unnecessarily, and servers/proxies may treat explicit false differently from an absent option.

**Fix:** return immediately after removal, or assign only in the non-default branch. Serialize booleans as lowercase invariant OData literals.

**Priority: P0 deterministic query-generation bug.**

### 2. **[FIXED]** String query parameters are removed and then immediately assigned again

The string overload performs:

```csharp
if (string.IsNullOrWhiteSpace(value)) queryString.Remove(name);
queryString[name] = value;
```

Therefore the stated “adds or removes” contract is not implemented. Depending on `QueryString` behavior, empty/null values may be emitted, normalized unexpectedly, or overwrite an existing parameter after removal.

**Fix:** return after removal and add explicit tests for null, empty, whitespace, and existing values.

**Priority: P1 URI correctness.**

### 3. **[FIXED]** Numeric OData options use the current culture

`$skip` and `$top` are serialized with `value.ToString()` rather than `CultureInfo.InvariantCulture`.

**Risk:** native digits or culture-specific formatting can produce invalid/non-portable OData URLs under non-English cultures.

**Fix:** use invariant formatting for every protocol value and test with cultures such as `ar-SA`, `fa-IR`, and `fr-FR`.

**Priority: P1 protocol correctness.**

### 4. Entity-set/table paths are concatenated without validation or segment escaping

The target is built as:

```csharp
new UriBuilderEx(UrlPrefix + "/" + query.Table)
```

There is no validation that the base URI is absolute HTTP(S), no normalization of trailing/leading slashes, and no distinction between an entity-set name and an arbitrary path/query/fragment supplied through `Table`.

**Risk:** malformed double slashes, broken URLs, path traversal-like composition, accidental query injection, and ambiguous treatment of entity-set names containing reserved characters.

**Fix:** parse and validate a base `Uri`, compose path segments through `UriBuilder`, define whether `Table` is a single entity-set identifier or an OData relative resource path, and validate/escape accordingly.

**Priority: P1 URL integrity and security boundary.**

### 5. **[FIXED]** `skip`, `Top`, and `Skip` accept invalid/overflowing values

`ODataQueryBuilder` computes `skip + (query.Skip ?? 0)` with ordinary `int` arithmetic and no validation. Negative values are accepted, and sufficiently large values can overflow before serialization.

**Risk:** invalid `$skip`/`$top` requests, wraparound to negative values, and inconsistent behavior between direct and paginated APIs.

**Fix:** validate all paging inputs as non-negative, require positive `$top` where appropriate, and use checked arithmetic or a wider internal type before enforcing OData/server limits.

**Priority: P1 paging correctness.**

### 6. Pagination ignores `@odata.nextLink` and reconstructs pages with `$skip`

`QueryToJSon` and the streaming path repeatedly clone the original query and increment the skip count. OData servers can return server-driven paging with an opaque `@odata.nextLink` that includes skip tokens, snapshots, partitions, or additional continuation state.

**Risk:** duplicates, missing rows, loops, or inconsistent results when data changes during enumeration, when the service uses `$skiptoken`, or when server paging semantics differ from simple offset paging.

**Fix:** prefer the response continuation link exactly as returned by the service. Use offset paging only as an explicit fallback for services known not to emit continuations. Validate that continuation URLs remain within the configured service origin/path policy.

**Priority: P1 OData protocol correctness.**

### 7. Forwarding an incoming request copies almost every header without a policy

`HttpGet` copies all source request headers and even content headers into the outgoing GET request using `TryAddWithoutValidation`. This can forward `Authorization`, `Host`, connection-specific headers, conditional headers, tracing headers, and unrelated content metadata.

**Risk:** credential/token leakage to another origin, invalid hop-by-hop headers, request smuggling/proxy ambiguity, incorrect caching/conditional behavior, and surprising coupling to the caller's inbound request.

**Fix:** replace blanket copying with an allowlist of explicitly supported context headers. Handle authorization and cookies through dedicated opt-in policies. Never forward `Host`, `Connection`, `Transfer-Encoding`, content headers for a bodyless GET, or arbitrary security credentials by default.

**Priority: P1 security and HTTP correctness.**

### 8. Cookie forwarding mutates a shared `CookieContainer` and uses `BaseUrl`, not the actual request URI

When a source cookie header is supplied, the code calls `CookieContainer.SetCookies(new Uri(BaseUrl), ...)`. The handler/container belongs to the `QueryOData` instance and is shared across requests. Concurrent calls can therefore persist and mix caller cookies, and redirects/subpaths may receive a cookie scope different from the actual outgoing URL.

**Risk:** cross-request/session cookie leakage, race conditions, incorrect cookie domain/path handling, and stale authentication state.

**Fix:** do not import per-request cookies into a shared handler container. Add the permitted cookie header directly to that request, use isolated clients/handlers per security context, or expose an explicit cookie policy. Scope cookies to the actual target URI.

**Priority: P1 session isolation and concurrency.**

### 9. **[FIXED]** Base URL and request URI validation are deferred and failures are swallowed in cookie handling

Constructors accept any non-null string. Cookie propagation catches every exception from URI parsing and silently continues, while the eventual request may fail later with a different exception.

**Risk:** diagnostics lose the original configuration error; invalid or unsupported schemes survive until deep inside execution.

**Fix:** validate and normalize the service base URI once in the constructor, store it as `Uri`, reject unsupported schemes, and remove broad exception swallowing.

**Priority: P1 configuration correctness.**

### 10. LINQ translation executes arbitrary captured expressions during query compilation

`EvaluateExpression` builds and compiles a lambda for every non-constant value expression, then invokes it. This can execute property getters, method calls, mutable closures, I/O, or other side effects while translating a query.

**Risk:** query translation is not observationally pure, errors occur at surprising times, user code can run repeatedly, and untrusted expression trees become a code-execution surface.

**Fix:** restrict evaluation to a safe, explicitly supported subset (constants and closure-field reads), or require callers to pre-evaluate values. Cache safe accessors only where justified and return structured unsupported-expression diagnostics.

**Priority: P1 expression safety and predictability.**

### 11. Binary translation assumes “member on the left, value on the right”

`TranslateBinary` always calls `TranslateMember(binary.Left)` and `TranslateValue(binary.Right)`. Valid LINQ such as `5 < entity.Quantity` is rejected rather than normalized to `Quantity gt 5`. Member-to-member comparisons are also not distinguished clearly.

**Fix:** inspect both operands, normalize reversible comparison operators when the member is on the right, and explicitly define support for member/member and value/value expressions.

**Priority: P1 LINQ semantic coverage.**

### 12. Enum literals are serialized as integers regardless of EDM semantics

The LINQ literal formatter converts every enum to its underlying `Int64`. OData enum values are normally represented with their EDM enum type/name semantics, and some services expose enums as strings rather than integers.

**Risk:** generated filters are rejected or silently compare against the wrong representation.

**Fix:** use metadata-aware literal conversion through one EDM converter registry. Define behavior for flags, nullable enums, generated CLR enums, and services that intentionally expose numeric fields.

**Priority: P1 type/protocol correctness.**

## Medium-priority findings

### 13. **[FIXED]** Empty successful result sets are returned as errors (both QueryToJSon and QueryToDataReader)

`QueryToJSon` returns error code 1 with “No data returned” when no rows are found; `QueryToDataReader` follows the same pattern. An empty collection is normally a successful OData query result, not an exceptional transport or protocol failure.

**Risk:** callers cannot distinguish a valid empty set from an actual failure without parsing custom error state; normal LINQ/data-access semantics are broken.

**Fix:** return an empty array/reader successfully and reserve errors for malformed payloads, HTTP failures, metadata failures, and cancellation.

**Priority: P2 API semantics.**

### 14. `ODataContext` performs synchronous network I/O in constructors

Remote metadata loading uses `GetAsync(...).GetAwaiter().GetResult()` and `ReadAsStreamAsync(...).GetAwaiter().GetResult()` from a constructor path.

**Risk:** blocked threads, deadlock sensitivity in custom synchronization contexts, poor cancellation, and constructor latency that can unexpectedly include a 30-second network wait.

**Fix:** separate context construction from asynchronous initialization, provide `CreateAsync`/metadata-provider factories, and accept `HttpClient` plus cancellation tokens through dependency injection.

**Priority: P2 asynchronous API design.**

### 15. Metadata stream loading is unbounded for caller-provided streams and duplicates the full payload in memory

The stream constructor copies the entire input into a `MemoryStream` with the default `int.MaxValue` limit. The remote path enforces 10 MiB, but local/caller streams do not. Deserialization therefore requires at least one full additional in-memory copy.

**Risk:** excessive allocation or denial of service from large streams, inconsistent size policy by source, and avoidable memory pressure.

**Fix:** enforce a configurable limit consistently, deserialize directly from seekable streams when possible, and use bounded buffering only when replay is actually required.

**Priority: P2 resource management.**

### 16. `ODataContext` creates a new `HttpClient` per metadata download

Each remote context construction creates and disposes a dedicated client/handler. This prevents handler pooling, centralized proxy/authentication configuration, resilience policies, and test substitution.

**Fix:** inject/reuse `HttpClient` or an `HttpMessageInvoker`; keep timeout and maximum-size policies at the operation level.

**Priority: P2 HTTP lifecycle and testability.**

### 17. `ODataQueryBuilder.Authorization` is documented but never populated

The property is initialized to null and no constructor logic extracts or sets authorization information.

**Risk:** dead/misleading API surface and callers may believe credentials embedded in the base URL are being handled separately.

**Fix:** remove the property, or implement a secure explicit authorization model. Do not encourage user-info credentials in URLs.

**Priority: P2 API accuracy.**

### 18. Query objects are mutable while asynchronous execution is in progress

`Query` exposes writable properties. Pagination reads some values once (`Top`) but clones or reads other values across later iterations. A caller mutating the same query object concurrently can produce mixed requests and inconsistent limits.

**Fix:** use immutable query records/builders or snapshot the entire `IQuery` at operation entry. Document thread-safety and ownership.

**Priority: P2 concurrency and reproducibility.**

### 19. HTTP success/error handling is split across layers and raw responses can escape undisposed

`SimpleQuery` returns a nullable/raw `HttpResponseMessage`, placing status validation and disposal on callers, while higher-level paths convert failures into `ReturnValue`. The return type is nullable even though the implementation returns a response or throws.

**Fix:** make nullability truthful, document ownership prominently, provide a structured response wrapper or ensure internal APIs own/dispose responses, and centralize OData error-payload parsing.

**Priority: P2 resource/API consistency.**

### 20. Package targets only `net9.0` at version `0.0.1` without a compatibility policy

The client library targets a single runtime and suppresses missing XML documentation warnings globally. The package contains public APIs and a LINQ provider but does not expose a stated compatibility/versioning policy.

**Fix:** decide supported TFMs intentionally, enable nullable analysis and API analyzers, avoid blanket CS1591 suppression where public documentation matters, and define protocol/runtime compatibility in the README.

**Priority: P2 packaging quality.**

## Duplications of intent to reduce

- URI option serialization should use one invariant OData query writer rather than three divergent overloads.
- CLR-to-EDM literal conversion should be centralized and shared by LINQ filters, keys, metadata conversion, and generated clients.
- HTTP request construction, allowed header forwarding, cookies, authorization, status validation, and OData error parsing should pass through one request pipeline.
- Pagination should use one continuation abstraction shared by JSON aggregation, data readers, and typed LINQ execution.
- Query snapshots/cloning are currently pagination-specific; immutable query state should be the single representation throughout compilation and execution.
- Metadata acquisition should use one asynchronous, bounded, injectable loader for streams, files, and HTTP sources.

## Required tests

- Verify omission/emission of null, whitespace, false, true, zero, negative, and culture-sensitive query options.
- Run URI-generation tests under multiple current cultures and with trailing/leading slashes, reserved table characters, fragments, and existing base queries.
- Test checked overflow for combined skip values and invalid top/skip inputs.
- Simulate server-driven paging with absolute and relative `@odata.nextLink`, `$skiptoken`, changing datasets, repeated links, and cross-origin continuation attempts.
- Verify no sensitive/hop-by-hop/content headers are forwarded unless explicitly allowed.
- Run concurrent requests with distinct cookies and prove no cross-request leakage.
- Test invalid base URLs at construction rather than request time.
- Use expression trees with side-effecting getters/methods and ensure unsupported evaluation is rejected without execution.
- Test reversed comparisons, nullable members, enums/flags, decimals, dates, GUIDs, strings with quotes, and non-finite floating-point values.
- Verify empty JSON and data-reader results are successful and retain schema/metadata when available.
- Test cancellation and timeout during metadata download without blocking constructors.
- Enforce metadata size limits for HTTP, files, seekable streams, and non-seekable streams.
- Mutate a query object during pagination and verify immutable snapshot semantics.
- Assert response and stream disposal on success, HTTP failure, JSON failure, cancellation, and consumer-abandoned streaming readers.
- Compile and execute every README example against a scripted fake OData service.

## Recommended order

| Priority | Action |
|---|---|
| P0 | Correct boolean/string query-option emission |
| P1 | Centralize invariant URI construction and paging validation |
| P1 | Follow `@odata.nextLink` safely instead of reconstructing offsets |
| P1 | Replace blanket header/cookie forwarding with explicit isolation policies |
| P1 | Restrict LINQ expression evaluation and centralize EDM literal formatting |
| P2 | Return empty results successfully and snapshot immutable queries |
| P2 | Introduce asynchronous injectable bounded metadata loading |
| P2 | Consolidate response ownership/errors and improve package contracts |

## Deployment warning

Until items 1–12 are addressed, generated URLs can contain unintended options or culture-dependent values, offset pagination can miss/duplicate data, request-context forwarding can leak headers/cookies, and LINQ compilation can execute arbitrary captured code. Use the client only with trusted query definitions, trusted expression trees, isolated single-user request contexts, invariant process culture, and services known not to require server-driven continuation links.