# Utils.Imaging — Quality and Security Audit (2026-07-11)

Static audit of `Utils.Imaging`, covering unsafe bitmap accessors, transformations, convolution, color models and drawing abstractions. Findings are not fixed unless explicitly stated.

## Critical findings

### 1. ~~Unsafe bitmap indexers allow out-of-bounds native memory access~~ ✅ FIXED
`BitmapAccessor`, `BitmapIndexed8Accessor` and `BitmapArgb64Accessor` calculate pointer offsets directly from caller-provided coordinates/components without range checks.

**Fix:** validate coordinates and components before every public access; retain only explicitly named internal unchecked paths after bounds have been proven; check disposed state.

**Priority:** P0.

### 2. ~~`BitmapArgb64Accessor` ignores bitmap stride~~ ✅ FIXED
The accessor addresses pixels with `y * Width + x` instead of deriving each row from `Scan0 + y * Stride`.

**Fix:** use stride-aware row addressing and define/test logical orientation for negative strides.

**Priority:** P0.

### 3. ~~Constructor failure can leave a bitmap locked~~ ✅ FIXED
`BitmapAccessor` calls `LockBits` before all post-lock validation is complete. A later exception can leak the lock because construction never completes.

**Fix:** validate first and make post-lock initialization transactional with guaranteed `UnlockBits` on failure.

**Priority:** P0.

## High-severity findings

### 4. ~~Accessors remain callable after disposal~~ ✅ FIXED
Public properties/indexers can dereference cleared native state.

**Fix:** explicit disposed state, idempotent disposal and `ObjectDisposedException` from all public members.

**Priority:** P1.

### 5. Locked regions and pointer arithmetic are insufficiently validated
Empty, negative or out-of-image regions and unchecked offset/size arithmetic are accepted or delegated to GDI+.

**Fix:** validate regions explicitly and use checked `long`/`nint` arithmetic.

**Priority:** P1.

### 6. ~~`MatrixImageTransformer` allocates from unchecked dimensions~~ ✅ FIXED
`new A[width * height]` uses unchecked multiplication and an unbounded full-image copy.

**Fix:** validate dimensions, use checked multiplication, enforce a pixel limit or accept caller-provided/tiled workspace.

**Priority:** P1.

### 7. ~~Mask dimensions are never validated~~ ✅ FIXED
A mask is indexed over the destination dimensions without requiring a matching size.

**Fix:** reject mismatched dimensions before modifying any destination pixel.

**Priority:** P1.

### 8. ~~Transformer weights can be mutable or non-finite~~ ✅ FIXED
The caller's `double[,]` is retained directly and `NaN`/infinity are accepted.

**Fix:** clone and validate a non-empty matrix into immutable internal storage.

**Priority:** P1.

### 9. ~~`ColorArgb64` to `ColorArgb` uses the wrong divisor~~ ✅ FIXED
16-bit channels are divided by `255.0` instead of `65535.0`.

**Fix:** divide by `ushort.MaxValue` and test exact endpoints/random round trips.

**Priority:** P1 functional bug.

### 10. ~~8-bit to 16-bit expansion is not range-preserving~~ ✅ FIXED
Current conversions use `value << 8`, mapping 255 to 65280 instead of 65535.

**Fix:** centralize exact binary replication:

```csharp
static ushort ExpandByte(byte value) =>
    (ushort)(((ushort)value << 8) | value);
```

Use it for every 8→16 conversion path.

**Priority:** P1 functional bug.

### 11. ~~Porter-Duff `Over` mixes straight and premultiplied formulas~~ ✅ FIXED
The floating, 8-bit and 16-bit implementations use different and incomplete formulas.

**Fix:** choose one documented representation and delegate every type to one tested generic implementation.

**Priority:** P1 functional bug.

### 12. ~~Convolution semantics normalize only positive weight sums~~ ✅ FIXED
Zero-sum and negative-sum kernels are skipped or normalized incorrectly.

**Fix:** separate convolution from weighted averaging; expose divisor, bias, normalization and border policies explicitly.

**Priority:** P1 functional bug.

### 13. ~~Drawing viewports accept zero, non-finite and otherwise degenerate ranges~~ ✅ FIXED
`DrawF` divides image dimensions by `Right - Left` and `Down - Top` without validating zero spans, `NaN` or infinity. This creates infinite/NaN ratios and undefined integer conversions later.

**Fix:** reject non-finite boundaries and zero-width/zero-height viewports; define whether reversed axes are supported and validate consistently.

**Priority:** P1.

### 14. ~~`DrawF` maps the declared right/bottom boundaries outside the image~~ ✅ FIXED
The default viewport uses `Right = Width` and `Down = Height`, while mapping uses `Width / (Right - Left)` and truncation. Therefore `x == Right` maps to `Width` and `y == Down` maps to `Height`, both outside valid indexes. The downstream point routine silently drops them.

**Fix:** define a pixel-center or edge-based coordinate contract. For an inclusive endpoint contract, scale to `Width - 1`/`Height - 1`; for a half-open viewport, document and test `[Left, Right)` and `[Top, Down)` explicitly.

**Priority:** P1 functional bug.

### 15. ~~Shape drawing divides by zero for zero-length drawables~~ ✅ FIXED
`DrawI.DrawShape` passes `point.Position / drawable.Length` to the brush. Empty or degenerate shapes can have length zero, producing `NaN`/infinity and undefined brush behavior.

**Fix:** handle zero-length shapes explicitly, either drawing a single point with position zero or rejecting them before brush evaluation.

**Priority:** P1.

### 16. ~~Scan-line filling is not clipped before iteration~~ ✅ FIXED
`FillShapeCore` derives `yStart`, `yEnd`, `xFrom` and `xTo` from arbitrary geometry and only clips at `DrawPoint`. Shapes far outside the image can therefore trigger enormous loops while producing no pixels.

**Risk:** CPU denial of service from untrusted or accidental extreme coordinates.

**Fix:** intersect scan-line and span ranges with image bounds before looping; reject non-finite coordinates and use checked conversions.

**Priority:** P1.

## Functional correctness and design findings

### 17. ~~Pixel-format aliases are treated as concrete layouts~~ ✅ FIXED
Flag-like values such as `Alpha`, `PAlpha` and `Canonical` are accepted as if they uniquely described storage.

**Fix:** support only tested concrete formats and validate with `Image.GetPixelFormatSize`.

**Priority:** P2.

### 18. ~~Premultiplied-alpha formats are exposed as straight ARGB~~ ✅ FIXED
PArgb formats are read and written with straight-alpha blending rules.

**Fix:** reject them in straight-color operations or introduce explicit premultiplied accessors/types.

**Priority:** P2.

### 19. ~~Sprite traversal is duplicated~~ ✅ FIXED
Sprite application exists in generic and specialized implementations.

**Fix:** retain one traversal contract and isolate only measured low-level optimizations.

**Priority:** P2 maintainability.

### 20. ~~Resource ownership and finalization are implicit~~ ✅ FIXED
Accessors leave bitmaps alive while finalizers call `UnlockBits` through managed objects at nondeterministic times.

**Fix:** standardize ownership, prefer deterministic disposal and minimize/remove finalizers where possible.

**Priority:** P2.

### 21. ~~`System.Drawing` platform constraints are not explicit~~ ✅ FIXED
The package is effectively Windows/GDI+ oriented on modern .NET.

**Fix:** target/document Windows or isolate the backend behind a cross-platform abstraction.

**Priority:** P2 compatibility.

### 22. ~~Packed color values depend on native endianness~~ ✅ FIXED
`ColorArgb32` and `ColorArgb64` overlay component fields and packed integers in host memory order.

**Fix:** define canonical values with shifts/masks or clearly separate native-layout and canonical packing APIs.

**Priority:** P2.

### 23. ~~`ColorArgb32(IColorArgb<byte>)` wraps valid component values~~ ✅ FIXED
The constructor multiplies byte components by 255 before casting back to byte.

**Fix:** copy byte values directly or replace it with correctly typed conversion overloads.

**Priority:** P2 functional bug.

### 24. Gradient APIs use inconsistent interpolation and factor validation
Floating and byte implementations use different spaces and different out-of-range behavior.

**Fix:** expose explicit interpolation modes with shared factor validation and conversion rules.

**Priority:** P2.

### 25. Numeric conversion and rounding policies are inconsistent
Some paths truncate, some round and some throw after partial image mutation.

**Fix:** centralize clamp/round/convert helpers and stage results before committing when conversion can fail.

**Priority:** P2.

### 26. ~~Fill topology is changed by a hard-coded 0.5-pixel merge threshold~~ ✅ FIXED
`FillShapeCore` merges adjacent scan-line intersections whenever their distance is below `0.5f`. Legitimate narrow features, close contours and small holes can be collapsed or cancelled solely because of scale.

**Fix:** do not use a global geometric-distance heuristic to alter winding topology. Resolve shared vertices from segment identity/rasterization rules, or make tolerance explicit and scale-aware.

**Priority:** P2 functional bug.

### 27. ~~Drawing abstractions do not validate null dependencies~~ ✅ FIXED
`BaseDrawing` stores a nullable-at-runtime accessor without checking it, and public drawing methods similarly assume non-null brushes, delegates and drawable collections.

**Fix:** validate constructor and public API dependencies immediately with precise `ArgumentNullException`s.

**Priority:** P2 API quality.

### 28. ~~Blur kernel size arithmetic can overflow before normalization~~ ✅ FIXED
`ConvolutionMatrixFactory.Blur` computes `size * size` as `int` and allocates a square array without an upper bound. Large sizes can overflow the divisor or cause uncontrolled allocation.

**Fix:** use checked `long`/`double` arithmetic, enforce a practical maximum kernel area and fail before allocation.

**Priority:** P2.

### 29. ~~HSV hue accepts non-finite values~~ ✅ FIXED
`ColorAhsv.Hue` applies modulo directly without rejecting `NaN` or infinity. Such values can survive into sector selection and produce invalid conversions.

**Fix:** reject non-finite hue values before normalization and apply the same policy to all floating color components.

**Priority:** P2.

## Required regression and security tests

- Fuzz every unsafe accessor with negative/boundary/oversized coordinates and components.
- Test positive/negative stride, row padding and subregions.
- Force post-`LockBits` constructor failures and verify unlock.
- Verify disposed-state behavior and idempotent disposal.
- Test checked image/kernel dimension arithmetic and configured limits.
- Reject mismatched masks and invalid/non-finite matrices before mutation.
- Golden-image tests for blur, sharpen, edge detection, zero/negative-sum kernels and border policies.
- Separate straight-alpha and premultiplied-alpha tests.
- Verify `ColorArgb64 → ColorArgb` endpoints.
- Exhaustively test `((ushort)value << 8) | value` for all 256 byte values, including `0→0`, `1→257`, `128→32896`, `255→65535`, and exact 8→16→8 round trips.
- Validate Porter-Duff reference vectors across all component types.
- Test canonical packed values independently of host endianness.
- Exercise all gradient modes and out-of-range factors.
- Reject zero/non-finite viewports and test reversed-axis policy.
- Verify exact mapping of all four viewport boundaries under the chosen inclusive/half-open contract.
- Test empty and zero-length drawables without non-finite brush positions.
- Use extreme off-screen geometry and assert work is bounded by image dimensions.
- Golden tests for sub-pixel-width contours and holes to ensure fill topology is preserved.
- Reject non-finite HSV components and verify stable ARGB↔HSV round trips.

## Priority roadmap

| Priority | Finding |
|---|---|
| P0 | Unsafe native indexers, stride errors and leaked bitmap locks |
| P1 | Disposed-state, region and arithmetic validation |
| P1 | Transformer allocation, masks and mutable/non-finite weights |
| P1 | 16-bit conversions and Porter-Duff compositing |
| P1 | Convolution semantics |
| P1 | Invalid viewport mapping and zero-length shape handling |
| P1 | Unclipped scan-line loops permit CPU exhaustion |
| P2 | Pixel formats, premultiplied alpha and packed endianness |
| P2 | Duplicated sprite traversal and ownership/platform contracts |
| P2 | Generic color constructor, gradients and numeric policy |
| P2 | Fill tolerance changes geometry topology |
| P2 | Null contracts, kernel limits and non-finite HSV values |

## Deployment warning

Until the P0 findings are fixed, do not expose bitmap coordinates, regions, formats or dimensions derived from untrusted input to these unsafe accessors. Until the drawing P1 findings are fixed, do not process untrusted geometry: extreme off-screen coordinates can consume excessive CPU even when no pixels are ultimately written.