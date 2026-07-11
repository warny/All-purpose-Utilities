# Utils.Xml — Quality and security audit (2026-07-11)

Static review of `XmlUtils` and `XmlDataProcessor`, focused on parser safety, XPath dispatch correctness, reader state, namespace handling, resource limits, API contracts, and duplicated intent. No production code is changed by this commit.

## Critical priority

### 1. `ReadChildElements` does not track XML depth correctly

Both overloads increment/decrement their local `depth` only when an encountered start/end element has the same qualified name as the original parent. Nested elements with different names leave `depth` unchanged and are yielded as if they were immediate children.

**Risk:** grandchildren and deeper descendants are incorrectly exposed as direct children. Processing logic can silently attach data to the wrong parent.

**Fix:** capture `reader.Depth` at entry and yield only elements whose `Depth == parentDepth + 1`. Stop when the reader reaches the matching end element at `parentDepth`, independently of element names.

**Priority: P0 functional correctness.**

### 2. `ReadChildElements` mishandles empty parent elements

For `<parent />`, the iterator still calls `Read()` and has no matching end element to terminate on. It may scan and yield unrelated elements from the remainder of the document.

**Risk:** cross-scope data leakage and unpredictable reader advancement.

**Fix:** return immediately when `reader.IsEmptyElement` is true. Add an explicit reader-position contract for both normal completion and early iterator disposal.

**Priority: P0 functional correctness.**

## High priority

### 3. Yielding the same mutable `XmlReader` makes enumeration behavior consumer-dependent

Each iteration yields the original reader instance. If the consumer reads the element body, skips a subtree, calls another reader helper, or only partially consumes the iterator, it mutates the state used by the outer iterator.

**Risk:** siblings may be skipped, descendants may be reclassified, and behavior changes depending on what the loop body does.

**Fix:** define one explicit model:
- streaming cursor API that requires the consumer to consume/skip the current subtree, implemented with `ReadSubtree()` and documented state transitions; or
- materialized child representation (`XElement`, `XmlElement`, immutable record) that isolates each result.

**Priority: P1 API contract.**

### 4. `XmlDataProcessor.Current` is not restored when a handler throws

`InvokeSingleNode` stores the previous context, assigns `Current`, invokes the handler, then restores the old value only on normal completion.

**Risk:** after an exception, the processor remains bound to the failed node. If the caller catches the exception and reuses the processor, `ValueOf`, `Apply`, and `GetNodes` operate on stale state.

**Fix:** restore `Current` in a `finally` block. Decide whether invocation exceptions should be unwrapped from `TargetInvocationException` while preserving the original stack.

**Priority: P1 correctness and lifecycle.**

### 5. Optional handler parameters are matched but not supplied

`ParametersMatch` accepts fewer arguments when the missing parameters are optional, but `MethodInfo.Invoke` receives the original shorter array. Reflection does not automatically append optional default values in this usage.

**Risk:** a handler can pass validation and then fail with `TargetParameterCountException`.

**Fix:** build the final invocation argument array, filling omitted optional entries with `ParameterInfo.DefaultValue` or `Type.Missing`. Reject unsupported optional metadata during construction.

**Priority: P1 functional correctness.**

### 6. Null arguments crash method selection before matching

Argument types are computed with `parameters.Select(o => o.GetType())`. A legitimate `null` argument causes `NullReferenceException`, even when the target parameter is nullable or a reference type.

**Risk:** supported handler signatures cannot receive null values, and failure diagnostics do not identify the argument or handler.

**Fix:** match using the argument values, not only `Type[]`. Treat null as compatible with reference types and nullable value types, and incompatible with non-nullable value types.

**Priority: P1 functional correctness.**

### 7. Handler selection is order-dependent and only the first match runs

Triggers are populated from reflection into a `Dictionary<XPathExpression, Method>`. `InvokeSingleNode` iterates that dictionary and stops after the first matching XPath/signature pair. Reflection order is not a stable public contract, and class documentation suggests that matching handlers are invoked rather than one arbitrary winner.

**Risk:** adding or recompiling a handler can change which method executes. Multiple intentional handlers for one node cannot coexist reliably.

**Fix:** define an explicit policy:
- invoke all matches in deterministic declaration/priority order; or
- reject ambiguous overlaps at construction; or
- require an explicit priority attribute and fail on ties.
Store triggers in an ordered immutable collection rather than using dictionary enumeration as dispatch precedence.

**Priority: P1 deterministic behavior.**

### 8. Handler signatures are not validated when the processor is constructed

Methods decorated with `[Match]` may be generic, static-like through unusual metadata, have `ref`/`out` parameters, pointer/byref-like types, unsupported optional values, or non-void return types. Errors occur only when a matching node is reached.

**Risk:** invalid processors appear successfully constructed and fail late on specific documents.

**Fix:** validate every handler once in the constructor and throw a descriptive configuration exception naming the method and XPath. Define the supported signature contract in the README.

**Priority: P1 robustness.**

### 9. `Read(Stream)` materializes untrusted XML without hardened settings or a size limit

`Read(Stream)` constructs `XPathDocument` directly from the stream. Unlike `ReadSecure(string)`, it does not apply the explicit DTD, resolver, entity, and document-size policy. It also loads the complete document before dispatch.

**Risk:** callers naturally treat a stream overload as safe for uploaded or network data, while large input can consume substantial memory and parser behavior depends on framework defaults.

**Fix:** add `ReadSecure(Stream)` and preferably make hardened settings the default for all raw-source overloads. Allow caller-supplied limits through an options object. Keep an explicitly named legacy/trusted path only when necessary.

**Priority: P1 security and resource limits.**

### 10. `ReadSecure(string)` accepts unrestricted URI schemes and performs I/O internally

The method is described as safe for untrusted XML, but the input is a URI passed directly to `XmlReader.Create`. Parser hardening does not prevent access to local files, UNC paths, loopback services, metadata endpoints, or remote hosts selected by an attacker.

**Risk:** SSRF/local-file access when the URI itself is user-controlled, despite the security-oriented method name.

**Fix:** separate parsing security from resource acquisition. Prefer `ReadSecure(Stream)` as the core API. For URI convenience overloads, require an explicit scheme/host policy, disable network access by default, and document that XML hardening is not an authorization boundary.

**Priority: P1 security.**

### 11. Configured XPath expressions can consume unbounded CPU

Attribute XPath expressions are compiled once, but `Apply(string)` and `GetNodes(string)` compile/evaluate arbitrary expressions repeatedly. XPath 1.0 evaluation has no cancellation or execution budget, and broad descendant queries can repeatedly scan a fully materialized document.

**Risk:** derived processors that compose XPath from external input can suffer XPath injection or high CPU usage; nested handlers can accidentally produce quadratic work.

**Fix:** discourage dynamic XPath composition, provide parameter-safe APIs for common name/attribute selections, cache trusted expressions with a bounded cache, and expose cancellation/document-node limits where feasible.

**Priority: P1 performance and security.**

## Medium priority

### 12. `GetXPath` does not always identify one unique node

An index is added only when preceding siblings with the same name exist. For the first of several same-name siblings, the generated path is `/root/item`, which selects all matching siblings rather than uniquely identifying the first one.

**Fix:** include `[1]` whenever another same-name sibling exists either before or after the element. Add an explicit option for compact non-unique paths versus unique paths.

**Priority: P2 correctness.**

### 13. `GetXPath` produces namespace-dependent paths without returning a namespace context

Paths use `XmlNode.Name`, which may contain document-specific prefixes. The returned XPath cannot be evaluated unless the caller reconstructs the exact prefix-to-URI mappings. Default-namespace elements are especially problematic because unprefixed XPath names match no namespace.

**Fix:** either return an object containing both the XPath and required namespace mappings, generate prefix-independent expressions using `local-name()`/`namespace-uri()`, or accept an `XmlNamespaceManager` and produce canonical prefixes.

**Priority: P2 API correctness.**

### 14. `GetXPath(XmlNode)` silently maps unsupported node kinds to their parent element

For text, CDATA, comment, processing-instruction, and other non-element nodes, the method returns the parent element path. Detached nodes may produce an empty string. The result therefore does not identify the requested node.

**Fix:** support explicit node tests and sibling indexes for relevant node kinds, or throw `NotSupportedException`. Reject detached nodes unless a documented relative-path mode is requested.

**Priority: P2 contract clarity.**

### 15. Namespace attribute validation does not match XML namespace rules

The prefix regex requires an ASCII letter first and then `\w`. Valid NCName prefixes beginning with `_` are rejected, while `\w` semantics are not a full XML NCName implementation. Empty/null namespace URIs are not validated, and duplicate prefixes from inherited attributes are delegated to `XmlNamespaceManager` without a domain-specific diagnostic.

**Fix:** use `XmlConvert.VerifyNCName`, reject reserved prefixes (`xml`, `xmlns`) unless their mandated URI is used, validate the namespace URI policy, and detect duplicate conflicting declarations with a clear exception.

**Priority: P2 validation.**

### 16. The alleged cached invoker still performs reflection for every call

`Method` stores an `Action`, but the action merely calls `MethodInfo.Invoke`. The `node` parameter is unused. This duplicates intent without delivering compiled invocation performance and adds another wrapping layer to debug.

**Fix:** either compile a real strongly typed expression/delegate that handles defaults and exceptions, or remove the wrapper claim and invoke `MethodInfo` directly through one centralized invocation path.

**Priority: P2 maintainability and performance.**

### 17. Nullability contracts are inconsistent with the implementation

`Current`, `ValueOf`, optional parameter arrays, and several method arguments are declared non-nullable while being null before processing or returning null on no match. This obscures lifecycle constraints and creates compiler warnings or false assumptions for consumers.

**Fix:** enable/observe nullable annotations consistently: `XPathNavigator? Current` internally with guarded protected access, `string? ValueOf`, `object?[]`, and explicit `ArgumentNullException` checks on public/protected inputs.

**Priority: P2 API quality.**

## Second-pass findings

### 18. A processor instance is not safe for concurrent or overlapping `Read` operations

`Current` is a single mutable instance property shared by all dispatches. Two threads, or two overlapping asynchronous operations using the same processor, can overwrite each other's current node. `NamespaceManager` and derived processor state are likewise exposed without a concurrency contract.

**Risk:** handlers can read values from another document or node, restore an obsolete context, and produce nondeterministic data corruption.

**Fix:** explicitly make processors single-operation and reject concurrent `Read` calls with an operation gate, or move invocation context to an immutable per-read session passed to handlers. Document whether derived state must be synchronized.

**Priority: P1 correctness and thread safety.**

### 19. Recursive redispatch can create unbounded cycles

A handler may call `Apply(".")`, select an ancestor or the document root, or otherwise dispatch a node that matches the same handler again. There is no active-frame tracking, recursion limit, or cycle detection.

**Risk:** accidental XPath cycles can cause stack overflow, repeated side effects, or unbounded CPU consumption even with a small XML document.

**Fix:** track dispatch depth and optionally active `(handler, node)` frames. Provide a configurable maximum depth and fail with a descriptive exception when a direct or indirect cycle is detected.

**Priority: P1 robustness and resource limits.**

### 20. Asynchronous handlers are invoked but never awaited

Handler return values are ignored. A method returning `Task` or `ValueTask` is accepted and invoked through reflection, but processing continues immediately and asynchronous exceptions become detached from the `Read` operation.

**Risk:** `Read` may return before processing is complete, `Current` may be restored while the handler still uses it, and failures can surface as unobserved task exceptions.

**Fix:** either reject every non-`void` handler eagerly, including async methods, or add a separate `XmlDataProcessorAsync` contract whose dispatch pipeline awaits `Task`/`ValueTask` handlers and carries context safely.

**Priority: P1 lifecycle correctness.**

### 21. Caller-provided `XmlReader` security settings are opaque and cannot be enforced

`Read(XmlReader)` wraps whatever reader it receives in `XPathDocument`. The processor cannot determine reliably whether DTD processing, external resolution, document limits, or other relevant settings were hardened by the caller.

**Risk:** an API surface that appears equivalent to `ReadSecure` may process hostile XML under permissive caller settings. Security behavior varies invisibly by construction path.

**Fix:** document `Read(XmlReader)` as a trusted/caller-policy overload, or introduce an API that accepts a source plus explicit processor-owned settings. Do not label processing secure solely because the source is already an `XmlReader`.

**Priority: P1 security contract.**

### 22. URI reads have no acquisition timeout, cancellation, or response-size policy before parsing

`ReadSecure(string)` delegates URI opening to `XmlReader.Create`. The parser has a character limit, but the API exposes no cancellation token, connection/read timeout, redirect policy, or pre-parse transport limit.

**Risk:** slow or stalled remote endpoints can block a thread for an uncontrolled duration. Redirects may cross trust boundaries even when the initial URI was approved.

**Fix:** move network acquisition outside the XML parser, use an explicitly configured HTTP client or caller-supplied stream, and expose cancellation and redirect/timeout policies.

**Priority: P1 availability and SSRF hardening.**

### 23. Inherited private handlers are silently omitted

Trigger discovery uses `GetMethods(Public | NonPublic | Instance)`. Private methods declared on base classes are not returned by this reflection call, despite `MatchAttribute` being marked `Inherited = true` and the framework presenting inheritance as supported.

**Risk:** refactoring a handler into a private base-class helper silently disables it. The attribute remains visible in source but no trigger is registered.

**Fix:** define inheritance semantics explicitly. Either walk the type hierarchy using `DeclaredOnly` and include private base methods intentionally, or reject/document private inherited handlers and remove misleading inheritance metadata.

**Priority: P2 deterministic configuration.**

### 24. Duplicate or conflicting namespace declarations have order-dependent diagnostics

Namespace attributes are read across the inheritance hierarchy and immediately passed to `XmlNamespaceManager.AddNamespace`. There is no normalization or pre-validation of duplicate prefixes, identical redeclarations, conflicting URIs, or reserved mappings.

**Risk:** constructor behavior depends on reflection attribute order, and consumers receive low-level exceptions without identifying the declaring type or conflicting attributes.

**Fix:** collect declarations first, normalize by prefix, allow exact duplicates only by explicit policy, reject conflicts deterministically, and report both declaring types and URIs.

**Priority: P2 configuration quality.**

### 25. `MatchAttribute` accepts null, empty, and whitespace XPath expressions until processor construction

The primary-constructor attribute stores its string without validation. Invalid values fail later in `XPathExpression.Compile`, detached from the attribute declaration and mixed with other constructor work.

**Risk:** poor diagnostics and avoidable late failures when attributes are generated or supplied indirectly.

**Fix:** validate null/empty/whitespace in the attribute constructor and wrap XPath compilation failures with the declaring method and expression. Keep semantic XPath validation in processor construction.

**Priority: P2 diagnostics.**

### 26. Public read and selection methods do not consistently validate null arguments or processor state

`Read(XPathNavigator)`, `Read(Stream)`, `Read(XmlReader)`, `Apply(XPathNavigator, ...)`, and selection helpers rely on downstream `NullReferenceException` or framework exceptions. `Apply`, `GetNodes`, and `ValueOf` also assume an active handler context.

**Risk:** callers receive inconsistent exceptions, and helper use outside a dispatch callback fails without explaining the required lifecycle.

**Fix:** add `ArgumentNullException.ThrowIfNull` at API boundaries and a guarded accessor that throws `InvalidOperationException` when no current dispatch context exists.

**Priority: P2 API contract.**

## Duplicated intent to reduce

- The two `ReadChildElements` overloads duplicate the complete traversal algorithm. Keep one core iterator with an optional predicate/name filter.
- Five `Apply`/`GetNodes` variants repeat XPath selection and dispatch. Centralize expression compilation, selection, null checks, and namespace usage.
- Parser security is currently tied only to `ReadSecure(string)`. Centralize source-independent `XmlReaderSettings` and make all raw-source overloads flow through it.
- Handler matching, default-argument construction, invocation, exception unwrapping, context restoration, recursion control, and async-policy validation should be one descriptor-level operation rather than split across `ParametersMatch`, `InvokeSingleNode`, and the pseudo-cached delegate.
- XPath path generation and namespace management should share a single canonical QName strategy.
- Input acquisition, cancellation, redirect policy, and parser limits should be separated instead of being partially bundled into `ReadSecure(string)`.

## Required tests

- Nested differently named elements must never be returned as immediate children.
- Empty parent elements must terminate without consuming later siblings or unrelated document content.
- Verify reader position after full enumeration, early break, subtree consumption, and exceptions in the loop body.
- Handler exceptions must restore `Current` in all cases.
- Optional parameters must receive defaults; null arguments must match nullable/reference parameters only.
- Multiple matching handlers must follow the documented deterministic policy; ambiguous ties must be tested.
- Invalid handler signatures must fail during processor construction.
- Secure stream parsing must reject DTDs, enforce document/entity limits, and avoid unrestricted URI fetching.
- Test deep/wide documents and repeated XPath evaluation under configured resource limits.
- The first, middle, and last repeated sibling paths must each re-select exactly one original node.
- Test prefixed namespaces, default namespaces, attributes, text, comments, processing instructions, and detached nodes.
- Test valid/invalid NCName prefixes, reserved prefixes, duplicate mappings, and null/empty namespace URIs.
- Run two `Read` calls concurrently on one processor and verify deterministic rejection or isolated contexts.
- Create direct and indirect redispatch cycles and verify a bounded diagnostic failure rather than stack overflow.
- Verify that `Task`/`ValueTask` handlers are either rejected or awaited according to the documented API.
- Test inherited protected/private handlers and overridden methods against the selected inheritance policy.
- Verify null arguments and helper calls outside an active handler produce explicit contract exceptions.
- Test remote acquisition cancellation, timeout, redirect rejection, and maximum-response handling outside the parser.

## Recommended order

| Priority | Action |
|---|---|
| P0 | Replace name-based depth tracking and handle empty elements |
| P1 | Define safe reader-state semantics for child enumeration |
| P1 | Make dispatch deterministic and restore context in `finally` |
| P1 | Correct null/optional argument binding and validate handlers eagerly |
| P1 | Prevent concurrent/cyclic dispatch and define the async-handler policy |
| P1 | Harden all raw XML inputs and separate URI acquisition from parsing |
| P1 | Define XPath resource/injection policy |
| P2 | Make generated XPath paths unique and namespace-aware |
| P2 | Align namespace, inheritance, lifecycle, and nullability validation with actual contracts |
| P2 | Remove pseudo-caching and consolidate duplicated traversal/dispatch code |

## Deployment warning

Until items 1–11 and 18–22 are addressed, do not use `ReadChildElements` for structurally sensitive streaming extraction without additional tests, and do not treat `ReadSecure(string)` as protection against hostile URI selection or stalled remote resources. `XmlDataProcessor` instances must be treated as single-use-at-a-time and synchronous: `Current` is mutable shared state, recursive redispatch is unbounded, and asynchronous handlers are not awaited.