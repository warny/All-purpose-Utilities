# Utils.VirtualMachine — Quality and correctness audit (2026-07-11)

> **Partially completed 2026-07-18/19.** Items 1, 2, 3, 11, 12 addressed in PR fix/utils-vm-quality-audit-p0-p1. Item 13 addressed in PR fix/utils-vm-quality-audit-round2. Items 4–10, 14–16 remain open.

Static review of the processor, scheduler, virtual-memory model, stacks, and structured control-flow helpers. The review focuses on deterministic bytecode dispatch, malformed-program handling, memory isolation, lifecycle state, resource bounds, and duplicated intent. No production code is changed by this commit.

## Critical priority

### ✅ 1. Prefix opcodes make longer instructions unreachable

The slow dispatch path reads bytes incrementally and invokes an instruction as soon as the accumulated byte sequence matches any registered opcode. If both `[0x10]` and `[0x10, 0x20]` are registered, the one-byte instruction executes immediately after `0x10`; the dispatcher never reads `0x20` to consider the longer opcode.

The fast lookup explicitly detects shared first bytes and routes them to the slow path, but the slow path still selects the first complete prefix rather than the longest valid match.

**Risk:** instruction-set behavior depends on whether one opcode is a prefix of another. Valid registered instructions can be permanently unreachable, and adding a longer opcode can silently fail to change dispatch semantics.

**Fix:** either reject prefix-conflicting opcode sets at registration/construction, or implement deterministic longest-match dispatch with lookahead and rollback semantics. Document the selected policy and validate it for both reflected and runtime registrations.

**Priority: P0 bytecode correctness.**

### ✅ 2. A scheduler exception can leave a process permanently `Running`

`Scheduler.Step` changes a process from `Ready` to `Running` before calling `ExecuteStep`. If instruction dispatch or a handler throws, no `finally` or fault transition restores the state. A later `Run` sees a `Running` process in its loop condition, while `Step` selects only `Ready` processes and returns `false` forever.

**Risk:** after a caught execution exception, subsequent scheduler execution can spin indefinitely with no runnable process. The process also has no explicit fault state or stored failure diagnostic.

**Fix:** wrap each quantum in a lifecycle guard. On exception, transition to a new `Faulted` state (or deterministically to `Terminated`), store the exception, and rethrow or report according to policy. Ensure `Run` cannot loop when no state transition is possible.

**Priority: P0 scheduler liveness.**

## High priority

### ✅ 3. Runtime opcode keys remain mutable after registration

`RegisterInstruction` inserts the caller-provided `byte[]` directly into a dictionary whose equality and hash code depend on the array contents. The caller can mutate the array afterward. The public `Instructions` enumeration also exposes the same key object as `IReadOnlyCollection<byte>`, which prevents mutation only through that interface, not through retained aliases.

**Risk:** mutating an opcode after insertion invalidates the dictionary's hash-bucket invariants, making entries unreachable, colliding with other opcodes, or producing inconsistent enumeration/dispatch behavior.

**Fix:** clone every opcode into immutable owned storage before registration and expose immutable copies or `ImmutableArray<byte>`. Use one canonical opcode value type for discovery, registration, lookup, diagnostics, and inspection.

**Priority: P1 deterministic dispatch.**

### 4. Instruction method signatures are only partially validated

Discovery checks only that a non-parameterless method's first parameter is exactly `T`. It does not explicitly reject generic methods, non-void return types, `ref`/`out` parameters, pointer/byref-like types, static methods, unsupported optional parameters, or unsupported operand types. Operand lookup uses `_numberReaderMethods[parameter.ParameterType]`, so an unsupported type fails with an unhelpful `KeyNotFoundException` during processor construction.

**Risk:** malformed processor classes fail late with reflection/expression-tree exceptions that do not identify the opcode and invalid signature clearly.

**Fix:** validate every attributed method and every opcode before compiling delegates. Require an explicit supported contract such as `void Method()` or `void Method(T, supported operands...)`, and throw a dedicated configuration exception naming the method, opcode, parameter, and reason.

**Priority: P1 configuration robustness.**

### 5. Operand-truncation handling can misclassify instruction-handler bugs

The dispatcher catches every `IndexOutOfRangeException` and `ArgumentOutOfRangeException` thrown by the handler and wraps it as truncated operand data. These exceptions may instead originate from the instruction's own domain logic, stack manipulation, collections, or user code.

**Risk:** real handler defects are reported as malformed bytecode, hiding the original failure category and misleading diagnostics or recovery logic.

**Fix:** have number-reading operations throw a dedicated `UnexpectedEndOfBytecodeException` carrying address and requested width. Catch only that exception at dispatch boundaries. Preserve unrelated handler exceptions unchanged or wrap them as execution faults with their original type and stack.

**Priority: P1 diagnostics and correctness.**

### 6. Inspector callbacks observe inconsistent instruction-pointer states

For unambiguous one-byte opcodes, the inspector is notified before the instruction pointer is advanced. In the slow path, opcode bytes are consumed first and the inspector is notified with the pointer already positioned after the opcode. Redirection checks consequently compare against different baselines.

**Risk:** inspectors, debuggers, and breakpoint handlers cannot rely on one documented instruction-pointer contract. The same logical instruction can expose different state solely because its opcode shares a prefix.

**Fix:** notify inspection at one consistent phase with explicit values for `instructionStart`, `opcodeLength`, and `operandStart`. Apply redirection before consuming any externally visible state, or provide a structured inspection context rather than inferring phase from `InstructionPointer`.

**Priority: P1 debugger contract.**

### 7. Signed negative virtual addresses are decomposed incorrectly

`VirtualProcess.Decompose` uses integer division and remainder directly. For signed address types, `-1 / pageSize` truncates toward zero and `-1 % pageSize` remains negative. `IsAccessible(-1)` can therefore report page zero as accessible, while `Read`/`Write` later use a negative span offset and throw a framework range exception instead of `MemoryAccessException`.

**Risk:** address validation is inconsistent, negative addresses can alias page-table entries during checks, and callers receive implementation exceptions instead of VM memory faults.

**Fix:** reject negative addresses explicitly for signed `TAddress`, including negative virtual page indexes. Centralize checked address decomposition and translate arithmetic/range failures into `MemoryAccessException` with the original address.

**Priority: P1 memory correctness.**

### 8. `MapPage` and `UnmapPage` do not verify process ownership

`VirtualMemory.MapPage` verifies that the physical page belongs to the memory instance, but does not verify that the target process belongs to it. `UnmapPage` performs no ownership check either. A process created by another `VirtualMemory<TAddress>` instance can therefore receive mappings to this instance's pages.

**Risk:** the memory-manager ownership boundary is bypassed, process lists no longer describe actual mappings, and pages can be shared across supposedly independent VM instances without an explicit operation.

**Fix:** require `_processes.Contains(process)` for every mapping operation and reject freed/foreign processes. Prefer embedding an immutable owner identity in `VirtualProcess` so validation is O(1) and cannot be bypassed by list inconsistencies.

**Priority: P1 isolation model.**

### 9. Cross-page writes are partially committed before an access fault

`VirtualProcess.Write` validates and writes one page chunk at a time. If a later page is unmapped or read-only, all earlier chunks have already modified physical memory.

**Risk:** callers may assume a failed write leaves memory unchanged, but receive a partially applied operation. This is especially hazardous for multi-byte values, pointers, headers, and synchronization structures crossing a page boundary.

**Fix:** define atomicity explicitly. For atomic writes, perform a complete validation pass over every touched page before copying any bytes. If partial writes are intentional, expose the number of bytes written or use a distinct streaming API and document the behavior.

**Priority: P1 memory consistency.**

### 10. Address, page, process, and instruction counters can overflow silently or fail late

`_nextPageId`, `_nextProcessId`, the scheduler process id, and the master virtual page index grow without an exhaustion policy. Incrementing a signed address type or integer identifier can wrap in unchecked contexts; narrow `TAddress` types can fail after pages were already allocated or registered.

**Risk:** duplicate identifiers, negative indexes, overwritten mappings, or partially committed allocation operations after long-running/high-churn use or deliberately small address types.

**Fix:** use checked increments and validate capacity before mutating lists or mappings. Define maximum process/page counts and a deterministic exhaustion exception. Consider reusable ids only with generation counters if lifecycle churn matters.

**Priority: P1 resource correctness.**

### ✅ 11. Removed scheduler processes may continue executing their current quantum

`Step` snapshots ready processes and checks membership only before starting each process. If an instruction handler calls `RemoveProcess` on its own process, the inner quantum loop does not recheck membership and continues executing up to `QuantumSteps` instructions.

**Risk:** code continues running after the scheduler reports the process removed; side effects occur without lifecycle visibility, and external cleanup may race with continued execution.

**Fix:** recheck registration/state after every instruction, or mark removal as a lifecycle transition observed by the quantum loop. Separate `RequestRemoval` from actual collection mutation while a process is running.

**Priority: P1 lifecycle correctness.**

### ✅ 12. `Break` and `Continue` destructively alter the stack before reporting misuse

Both methods pop nested blocks while searching for a loop. If no loop exists, they throw only after all blocks have been removed.

**Risk:** catching the exception and continuing execution leaves the control-flow stack corrupted. Exception/finally and conditional scopes disappear merely because malformed bytecode attempted `BREAK` or `CONTINUE`.

**Fix:** first locate the nearest loop without mutation, then commit the required pops only after validation succeeds. More generally, make malformed-control-flow operations transactional.

**Priority: P1 control-flow integrity.**

### ✅ 13. Exception blocks may be created with neither catch nor finally target

`PushException` accepts both target addresses as nullable without validation. `Throw` assumes that when there is no finally target, `CatchAddress` is non-null and dereferences `CatchAddress!.Value`.

**Risk:** malformed bytecode causes a generic nullable-value exception after the control-flow stack has already been mutated, rather than a deterministic VM validation fault.

**Fix:** reject exception blocks with neither target when they are pushed or, preferably, validate structured control flow before execution. Remove null-forgiving dereferences from runtime control-flow transitions.

**Priority: P1 malformed-bytecode handling.**

## Medium priority

### 14. `ControlFlowStack` has no maximum depth

Unlike `CallStack`, structured blocks can be pushed without any bound. Malicious or malformed bytecode can grow the stack until process memory is exhausted.

**Fix:** add a configurable maximum depth and a dedicated overflow exception. Align call-stack, operand-stack, and control-flow-stack resource policies under one VM limits object.

**Priority: P2 resource limits.**

### 15. VM limits and fault policies are scattered across unrelated classes

Call depth has a limit, control-flow depth does not, processor execution has cancellation but no instruction budget, scheduler uses a quantum but no total budget, virtual memory has page size but no page/process cap, and stack/memory faults use unrelated exception types.

**Risk:** embedding applications cannot establish one coherent sandbox policy, and each new VM component may invent different defaults and failure semantics.

**Fix:** introduce an immutable `VirtualMachineLimits`/execution policy containing instruction budget, call/control/operand stack depths, page/process limits, and cancellation/fault behavior. Keep low-level classes usable independently but allow them to consume the shared policy.

**Priority: P2 architecture.**

### 16. Public collections are live views over mutable internal state

`Instructions`, `Breakpoints`, `Pages`, `Processes`, and process `Mappings` expose live mutable state or enumerations. Even when collection mutation is blocked by the static type, concurrent registration/allocation/removal can invalidate enumeration. `Breakpoints` is directly mutable with no synchronization or validation.

**Fix:** document snapshot versus live-view semantics and return immutable snapshots where deterministic inspection matters. Route breakpoint changes through methods if processor execution may overlap with debugger control.

**Priority: P2 API clarity.**

## Duplicated intent to reduce

- Reflected and runtime instruction registration should share one canonical validation and immutable-opcode registration path.
- Fast and slow dispatch paths duplicate notification, redirection, handler invocation, and exception translation with different state timing. Resolve the opcode first, then execute one common dispatch pipeline.
- Address decomposition, accessibility checks, range validation, and fault construction should use one checked translation helper.
- Scheduler state transitions are spread across `Scheduler` and public methods on `ScheduledProcess`; centralize them in a validated state machine.
- Call-stack and control-flow-stack limits/faults should share one resource-limit vocabulary.

## Required tests

- Register `[0x10]` and `[0x10, 0x20]` in both orders and verify the documented rejection or longest-match behavior.
- Mutate an opcode source array after registration and verify dispatch remains unchanged.
- Validate generic, non-void, static, `ref`/`out`, byref-like, and unsupported operand handler signatures with descriptive construction failures.
- Ensure a handler-thrown `ArgumentOutOfRangeException` is not reported as truncated bytecode.
- Verify inspector callbacks see identical instruction-pointer phases for fast and slow opcode paths.
- Test `-1`, the minimum signed address, boundary-crossing arithmetic, and narrow address types.
- Reject mapping/unmapping with foreign and freed processes.
- Verify failed cross-page writes are either fully atomic or report documented partial progress.
- Exercise id/address exhaustion without partial registration.
- Remove a process from inside its handler and verify no subsequent instruction executes.
- Force an instruction exception, then call `Run` again and verify deterministic fault handling rather than an infinite loop.
- Execute `BREAK`/`CONTINUE` outside a loop while nested in other blocks and verify the stack remains unchanged.
- Reject exception blocks without catch/finally targets.
- Enforce call/control/instruction/page limits with boundary tests.

## Recommended order

| Priority | Action |
|---|---|
| P0 | Reject prefix ambiguity or implement longest-match opcode dispatch |
| P0 | Make scheduler exceptions transition processes out of `Running` |
| P1 | Own immutable opcodes and validate instruction signatures centrally |
| P1 | Separate operand-underflow faults from handler exceptions |
| P1 | Normalize inspector timing and dispatch state |
| P1 | Reject negative/overflowing addresses and foreign processes |
| P1 | Define cross-page write atomicity and lifecycle-safe removal |
| P1 | Make malformed control-flow operations transactional |
| P2 | Introduce coherent VM resource limits and snapshot semantics |

## Deployment warning

Until items 1–13 are addressed, do not treat arbitrary registered instruction sets or bytecode as safely validated input. Prefix-conflicting opcodes can dispatch incorrectly, malformed memory addresses and control-flow operations can corrupt runtime state, and scheduler recovery after an instruction exception can become non-terminating. Virtual-memory instances also do not currently enforce process ownership at mapping boundaries.