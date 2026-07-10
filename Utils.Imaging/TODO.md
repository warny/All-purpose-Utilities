# Utils.Imaging — Quality and Security Audit (2026-07-11)

Static audit of `Utils.Imaging`, covering unsafe bitmap accessors, image transformations, convolution helpers, color models and drawing abstractions. Security and memory safety are treated as primary concerns. Findings are not fixed unless explicitly stated.

## Critical findings

### 1. Unsafe bitmap indexers allow out-of-bounds native memory access
`BitmapAccessor`, `BitmapIndexed8Accessor` and `BitmapArgb64Accessor` calculate pointer offsets directly from caller-provided `x`, `y` and component indexes without checking their ranges. Negative or oversized coordinates and an invalid component index can read or write outside the locked bitmap region.

**Risk:** native memory corruption, process crashes, disclosure of adjacent memory and potentially exploitable behavior when coordinates can be influenced by untrusted image metadata or callers.

**Fix:** validate `0 <= x < Width`, `0 <= y < Height` and `0 <= component < ColorDepth` before every pointer access. Keep an explicitly named unchecked internal fast path only for loops that have already proven their bounds. Add disposed-state checks before dereferencing pointers.

**Priority:** P0.

### 2. `BitmapArgb64Accessor` ignores bitmap stride
The 64-bit accessor addresses pixels with `y * bmpdata.Width + x`. GDI+ scanlines are separated by `BitmapData.Stride`, which can contain padding and can be negative for bottom-up images. Width-based addressing therefore accesses the wrong row whenever stride differs from `Width * 8`.

**Risk:** cross-row corruption and out-of-bounds access, especially for negative-stride bitmaps or externally supplied bitmap layouts.

**Fix:** calculate each row from `Scan0 + y * Stride`, then index the `ulong` within that row. Define and test the intended logical top-to-bottom orientation for negative strides.

**Priority:** P0.

### 3. Constructor failure can leave a bitmap permanently locked
`BitmapAccessor` calls `LockBits` before validating the selected pixel format with `GetColorDepth`. If the format is unsupported, `GetColorDepth` throws after the bitmap has been locked, while the partially constructed object cannot be disposed normally.

**Risk:** leaked native/GDI resources and subsequent failures when the same bitmap is accessed or disposed.

**Fix:** validate all arguments and supported formats before `LockBits` where possible. Otherwise wrap post-lock initialization in `try/catch` and always call `UnlockBits` on failure. Apply the same transactional construction pattern to every accessor.

**Priority:** P0.

## High-severity findings

### 4. Accessors remain callable after disposal
Disposal nulls native pointers and `BitmapData`, but public properties and indexers do not check an explicit disposed flag. Calls after disposal can produce `NullReferenceException` or dereference a null native pointer.

**Fix:** maintain `_disposed`, throw `ObjectDisposedException` from all public members, and make disposal idempotent. Avoid relying on nullable fields as the lifecycle model.

**Priority:** P1.

### 5. Locked regions and arithmetic are insufficiently validated
Constructors accept arbitrary rectangles. Width, height, offset and byte calculations can overflow or rely entirely on downstream GDI+ validation. `totalBytes = Stride * Height` can overflow and is unused.

**Fix:** reject empty/negative/out-of-bitmap regions explicitly; use checked `nint`/`long` arithmetic for row and pixel offsets; remove unused byte counts or use validated absolute stride calculations.

**Priority:** P1.

### 6. `MatrixImageTransformer` can allocate from unchecked image dimensions
The transformer allocates `new A[width * height]` using unchecked `int` multiplication. Invalid or very large accessor dimensions can overflow to a smaller/negative length or cause uncontrolled memory pressure.

**Fix:** require non-negative dimensions, use checked multiplication, enforce a configurable maximum pixel count or caller-provided workspace, and consider row/tile buffering for large images.

**Priority:** P1.

### 7. Mask dimensions are never validated
When a mask is supplied, the transformer indexes `mask[x, y]` for the full destination size without confirming matching dimensions.

**Risk:** exceptions or native memory corruption when the mask is an unsafe accessor smaller than the destination.

**Fix:** require identical dimensions before processing, or define an explicit clipping/resampling policy.

**Priority:** P1.

### 8. Transformer inputs allow non-finite or mutable weights
The constructor retains the caller's mutable `double[,]` and does not reject `NaN` or infinity. The matrix may be modified concurrently while a transformation is running.

**Risk:** nondeterministic results, conversion exceptions and partial image mutation.

**Fix:** validate dimensions and every weight, clone into immutable internal storage, and reject non-finite values.

**Priority:** P1.

### 9. `ColorArgb64` to `ColorArgb` conversion uses the wrong divisor
The floating-point constructor `ColorArgb(ColorArgb64 color)` divides 16-bit channels by `255.0` instead of `65535.0`. Any channel above 255 yields a value greater than 1 and therefore violates the constructor's own `[0,1]` validation.

**Risk:** almost every non-dark 16-bit color conversion throws or produces unusable values. This breaks implicit conversion paths and can abort image processing midway.

**Fix:** divide all four channels by `ushort.MaxValue`; add exact endpoint and randomized round-trip tests.

**Priority:** P1 functional bug.

### 10. 8-bit to 16-bit color expansion is not range-preserving
`ColorArgb64(ColorArgb32)` and `ColorArgb64(System.Drawing.Color)` expand channels with `value << 8`. This maps `255` to `65280`, not `65535`, and all intermediate values are biased low. Exact 8-to-16 expansion should replicate the byte (`value * 257`, equivalently `(value << 8) | value`).

**Risk:** white is no longer full white, opaque alpha is not fully opaque and repeated 8↔16 conversions lose precision systematically.

**Fix:** use `value * 257`; add exhaustive tests for all 256 input values and verify round-trip stability.

**Priority:** P1 functional bug.

### 11. Porter-Duff `Over` implementations mix straight and premultiplied formulas
`ColorArgb`, `ColorArgb32` and `ColorArgb64` compute output alpha and RGB with inconsistent formulas. The floating and 8-bit versions multiply the foreground by alpha but do not correctly include background alpha or divide by output alpha for straight-color output. The 64-bit implementation computes an `alpha` argument but does not use it in the color formula and also omits background alpha.

**Risk:** semi-transparent compositing produces incorrect colors and halos; the same logical operation yields different results depending on component type.

**Fix:** choose and document one representation: straight alpha or premultiplied alpha. Implement a single generic, numerically stable Porter-Duff formula and have every color type delegate to it. Add reference-vector tests against known compositing results.

**Priority:** P1 functional bug.

## Functional correctness and design findings

### 12. Convolution semantics incorrectly normalize by positive weight sum only
`MatrixImageTransformer` divides every accumulated component by `sumW` only when `sumW > 0`. Edge-detection, sharpening and other valid kernels commonly have zero or negative sums. Such pixels are silently left unchanged or normalized incorrectly.

**Fix:** separate convolution from normalized weighted averaging. Provide explicit policies such as `NoNormalization`, `NormalizeBySum`, divisor/bias and border handling. Do not infer semantics from the sign of the weight sum.

**Priority:** P1 functional bug.

### 13. Pixel-format aliases are treated as concrete storage formats
`BitmapAccessor.ColorDepths` includes flag-like or alias values such as `PixelFormat.Alpha`, `PAlpha` and `Canonical`. These do not necessarily describe a lockable concrete byte layout by themselves.

**Fix:** support only explicitly tested concrete formats and validate them with `Image.GetPixelFormatSize`; reject flags and aliases that do not uniquely define memory layout.

**Priority:** P2.

### 14. Premultiplied-alpha formats are exposed as straight ARGB
`BitmapAccessor` accepts `Format32bppPArgb` and `Format64bppPArgb`, but sprite blending reads and writes components as ordinary straight-alpha colors. Premultiplied data requires different conversion and blending rules.

**Risk:** dark fringes, incorrect colors and invalid premultiplied pixels.

**Fix:** either reject premultiplied formats in generic straight-ARGB operations or introduce explicit premultiplied color accessors and blending functions.

**Priority:** P2.

### 15. Duplicate sprite-blending implementations
Sprite application exists in `BitmapAccessor`, `BitmapArgb64Accessor` and generic `ImageAccessorExtensions`. The specialized implementations duplicate clipping and blend traversal and can diverge in behavior.

**Fix:** retain one generic traversal implementation and expose optimized internal pixel primitives where profiling justifies specialization. Test all accessors against the same behavioral contract.

**Priority:** P2 maintainability.

### 16. Resource ownership is implicit and inconsistent
Accessors always leave the bitmap alive, but this is not represented through a `leaveOpen`/ownership option. Finalizers call `UnlockBits` through managed `Bitmap` state, making cleanup timing nondeterministic.

**Fix:** document and standardize ownership, prefer deterministic disposal, and consider removing finalizers after ensuring callers use scoped access. If a finalizer remains, keep it minimal and robust against shutdown/disposed bitmap state.

**Priority:** P2.

### 17. `System.Drawing` platform constraints need to be explicit
The package is built around `System.Drawing.Bitmap` and GDI+ locking. Modern .NET support is effectively Windows-oriented for `System.Drawing.Common`.

**Fix:** target/document Windows explicitly or isolate the GDI implementation behind abstractions and provide a cross-platform backend. Add platform guards and CI coverage matching supported targets.

**Priority:** P2 compatibility.

### 18. Packed color layouts are architecture-dependent
`ColorArgb32` and `ColorArgb64` use explicit field overlays whose byte/component interpretation assumes little-endian memory layout. The public `Value` property is therefore not a portable canonical ARGB integer representation across architectures.

**Risk:** serialized packed values and component access can disagree on big-endian platforms or when exchanged with APIs expecting canonical `0xAARRGGBB` ordering.

**Fix:** define `Value` explicitly using shifts/masks independent of host endianness, or document it as native-layout-only and expose separate canonical pack/unpack APIs.

**Priority:** P2.

### 19. Color conversion naming and generic constructor intent are misleading
`ColorArgb32(IColorArgb<byte> colorArgb64)` accepts byte components but multiplies each by 255 before casting back to byte, which wraps for most values. The parameter name also claims a 64-bit source although the interface uses `byte`.

**Risk:** silent corruption when this constructor is called through the interface.

**Fix:** either copy byte components directly or replace the constructor with correctly typed overloads. Rename parameters to match actual types and add analyzer/tests preventing narrowing/wrapping conversions.

**Priority:** P2 functional bug.

### 20. Gradient APIs use inconsistent interpolation semantics and validation
`ColorArgb.Gradient` clamps the factor and performs square-space interpolation, while `ColorArgb32.LinearGradient` neither clamps nor rejects factors outside `[0,1]` and converts unchecked results to bytes. The naming does not make the color space or gamma behavior explicit.

**Risk:** out-of-range factors wrap or truncate, and equivalent APIs produce visibly different results.

**Fix:** define explicit interpolation methods (`LinearRgb`, `Srgb`, `Perceptual`) with common factor validation and shared conversion/rounding rules.

**Priority:** P2.

### 21. Numeric conversion policy is inconsistent
Conversions from floating components to byte/ushort use truncating casts rather than a documented rounding strategy. `MatrixImageTransformer` rounds integral values, while color constructors truncate. `T.CreateChecked` can also throw after partial image mutation.

**Fix:** centralize clamp/round/convert helpers and validate all output ranges before mutating the destination. Prefer staging a row/tile before commit when a conversion can fail.

**Priority:** P2.

## Required regression and security tests

- Fuzz every accessor indexer with negative, boundary and oversized coordinates/components; assert deterministic managed exceptions and no native crash.
- Test positive and negative stride layouts, row padding and locked subregions for every supported pixel format.
- Force constructor failures after `LockBits` and verify the bitmap is always unlocked.
- Verify all public members throw `ObjectDisposedException` after disposal and disposal is idempotent.
- Test checked dimension arithmetic and configured maximum pixel counts.
- Reject mismatched mask dimensions before any destination pixel is modified.
- Reject `NaN`/infinite matrices and verify caller mutation of the original matrix cannot affect a running transformer.
- Add golden-image tests for blur, sharpen, edge-detection, zero-sum and negative-sum kernels with explicit border/normalization policies.
- Test straight-alpha and premultiplied-alpha formats separately.
- Run common sprite behavior tests through generic, 32-bit and 64-bit accessors.
- Verify `ColorArgb64 → ColorArgb` endpoints: 0 maps to 0 and 65535 maps to 1.
- Exhaustively test 8→16 expansion for all channel values and exact 8→16→8 round trips.
- Validate Porter-Duff `Over` against reference vectors for transparent, opaque and partially transparent foreground/background combinations across all color types.
- Test canonical packed ARGB values independently of native memory layout.
- Exercise the `IColorArgb<byte>` constructor with every byte value and ensure no wraparound.
- Test gradient factors below 0, within range and above 1 with explicit interpolation semantics.

## Priority roadmap

| Priority | Finding |
|---|---|
| P0 | Unsafe indexers permit native out-of-bounds access |
| P0 | 64-bit accessor ignores stride |
| P0 | Constructor exceptions can leak bitmap locks |
| P1 | Use-after-dispose is not guarded |
| P1 | Region and pointer arithmetic validation is incomplete |
| P1 | Transformer allocation and mask dimensions are unchecked |
| P1 | Weight matrices allow mutable/non-finite data |
| P1 | 16-bit floating conversion uses an invalid divisor |
| P1 | 8-bit to 16-bit expansion loses full range |
| P1 | Porter-Duff compositing is inconsistent and incorrect |
| P1 | Convolution normalization semantics are incorrect |
| P2 | Pixel-format aliases and premultiplied formats are mishandled |
| P2 | Sprite traversal is duplicated |
| P2 | Ownership/finalization and platform support need clarification |
| P2 | Packed values depend on native endianness |
| P2 | Generic byte-color constructor wraps values |
| P2 | Gradient and numeric conversion policies are inconsistent |

## Deployment warning

Until the P0 findings are fixed, do not expose bitmap coordinates, regions, formats or dimensions derived from untrusted input to these unsafe accessors. A managed bounds exception is not guaranteed: current code can read or write native memory outside the locked image buffer. Until the P1 color-conversion findings are fixed, avoid relying on implicit 16-bit conversions or semi-transparent compositing for correctness-sensitive image pipelines.