# Paint.NET glitch1 CodeLab Effect Plugin — Agent Guide

**Project**: `glitch1` — A deterministic, seed-driven Paint.NET CodeLab effect plugin that applies realistic data-corruption visual glitches.

**Tech Stack**: C# | Paint.NET CodeLab ClassicEffect API | Deterministic Hashing | JPEG Codec Integration

---

## Core Architecture

The plugin implements a **composable multi-effect glitch engine** with the following design patterns:

### 1. ClassicEffect API Pattern
- **Render signature**: `Render(Surface dst, Surface src, Rectangle rect)`
- **UI bindings**: Use CodeLab `Amount1..Amount9` variables (maps to Seed, effect checkboxes, Stack mode)
- **Execution model**: Threadable region-based rendering; all randomness must be **deterministic and coordinate-based**, not global state
- **Entry point**: [glitch1.codelab.cs → Render()](src/glitch1.codelab.cs#L20)

### 2. Deterministic Randomness (Core Pattern)
**All randomness is derived from:** `Hash(seed, x/blockX, y/blockY, salt)`

- **Never use `System.Random()`** — destroys determinism and thread-safety
- **Hash function** [line 920]: Integer avalanche hash (xorshift-based). Input: `(seed, block_x, block_y, effect_salt)` → output: `uint`
- **Usage**: 
  ```csharp
  uint roll = Hash(seed, effectId, 0, 239) % 100u;
  double noise = Random01(seed, x, y, salt);  // normalized to [0, 1)
  ```
- **Design principle**: Same seed + same UI settings = identical output, regardless of render tile order

### 3. Effect Composition & Effect Order
- **Effect pool**: 7 glitch effects (JPEG Block Panic, Missing Packets, Channel Swap, Lack Of Clamping, Memory Alignment Tear, Single Channel Transform, JPEG Binary Damage)
- **Selection logic** [line 126]: Seed picks active effects from the allowed checkboxes; if none selected, fallback to first allowed
- **Stacking mode** [line 333]: When enabled, seed shuffles effect order via Fisher-Yates shuffle; when disabled, fixed UI order
- **Composition pattern**: Each effect operates on accumulated `result` from prior effects — not independent overwrites

### 4. Surface & Memory Management
- **Avoid allocations in render loop**: No `new` or `new Surface()` per-pixel
- **Pre-allocate surfaces**: [line 77, 80] `jpegSurface`, `damagedJpegSurface` allocated once before loop
- **Sampling**: Always clamp bounds with `SampleClamped(src, x, y)` [line 907] to prevent out-of-bounds reads
- **Alpha preservation**: All effects preserve `a.A` (alpha channel) unless explicitly modifying; JPEG operations use `original.A` fallback

### 5. Mask Feature (Spatial Gating)
- **Purpose**: Spatially modulate effect intensity (allows local masking without tiling artifacts)
- **Composition**: `GetMaskValue(x, y, width, height, seed, maskScale, maskPhase)` [line 847]
  - Layered sine waves (3 frequencies)
  - Seeded block-based turbulence (pseudo-Perlin)
  - Soft-threshold interpolation (smoothstep) → output ∈ [0, 1]
- **Usage**: Applied when mask-enabled effects active; ignored otherwise

---

## Key Code Patterns & Anti-Patterns

### ✅ Pattern: Seed-Derived Parameters
Avoid UI bloat by deriving hidden parameters from seed:

```csharp
double intensity = 0.45 + (Random01(seed, 0, 0, 101) * 0.45);  // [0.45, 0.90]
int blockSize = 4 + ((int)(Hash(seed, 0, 0, 103) % 8u) * 2);   // [4, 18]
```
**Why**: Keeps UI clean; ensures determinism; all variation encoded in seed value.

### ✅ Pattern: Effect-Specific Salts
Use unique `salt` parameter for each effect/operation to avoid hash collisions:

```csharp
// ✅ Different salts prevent interference
uint roll0 = Hash(seed, effectId, 0, 239) % 100u;  // Effect selection
uint roll1 = Hash(seed, effectId, 0, 241) % 100u;  // Different outcome
```

### ❌ Anti-Pattern: Unguarded Surface Access
```csharp
// ❌ UNSAFE: Could read/write out of bounds
dst[x, y] = src[x + offset, y + offset];

// ✅ SAFE: Always clamp
dst[x, y] = SampleClamped(src, x + offset, y + offset);
```

### ❌ Anti-Pattern: Global Random State
```csharp
// ❌ NOT allowed — breaks determinism and thread-safety
Random rand = new Random(seed);
return rand.Next(100);

// ✅ Deterministic coordinate hash
return (int)(Hash(seed, x, y, salt) % 100u);
```

### ✅ Pattern: Color Channel Blending with Alpha Preservation
```csharp
// ✅ Use `BlendPreserveAlpha()` for all effect blending
result = BlendPreserveAlpha(result, effectOutput, amount);
// Preserves original `result.A`; blends RGB by linear interpolation
```

---

## File Structure & Entry Points

| File | Purpose |
|------|---------|
| [src/glitch1.codelab.cs](src/glitch1.codelab.cs) | Single-file CodeLab implementation; 950+ lines |
| [GLITCH_EFFECT_PLUGIN_PLAN.md](GLITCH_EFFECT_PLUGIN_PLAN.md) | Spec: requirements, UI controls, design rationale |
| [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) | Progress tracking; implementation milestones |

**Key function ranges** in [src/glitch1.codelab.cs](src/glitch1.codelab.cs):
- UICode bindings: Lines 1–18
- `Render()`: Lines 20–117 (main entry point and early-out paths)
- Effect selector: [SeedSelectEffect()](src/glitch1.codelab.cs#L126)
- JPEG round-trip: [EncodeSurfaceAsJpegBytes()](src/glitch1.codelab.cs#L357), [DecodeJpegBytesToSurface()](src/glitch1.codelab.cs#L383)
- Hash & randomness: [Hash()](src/glitch1.codelab.cs#L920), [Random01()](src/glitch1.codelab.cs#L914)
- Helper utilities: [ClampInt(), WrapByte(), SampleClamped()](#)

---

## Common Development Tasks

### Task: Add a New Glitch Effect
1. **Define effect function** (e.g., `ColorBgra MyNewEffect(...)`) with deterministic hash-based randomness
2. **Add effect ID**: Assign next unused integer to `effectOrder[]` in `GetEffectOrder()`
3. **Register in effect pool**: Add checkbox control in `UICode` (e.g., `Amount10`)
4. **Wire in dispatcher**: Add condition in `ApplyEffectById()` [line 327]
5. **Seed-select probability**: Add case in `SeedSelectEffect()` [line 126] to control activation frequency
6. **Test**: Verify determinism by rendering same seed twice; use low seed values for quick iteration

### Task: Modify Mask Behavior
- Edit `GetMaskValue()` [line 847] to adjust wave frequencies, turbulence scale, or interpolation curve
- Use `seed, maskScale, maskPhase` parameters derived from seed
- Keep output in [0, 1] range and smooth (avoid sharp transitions that cause banding)

### Task: Adjust JPEG Quality or Corruption Strategy
- **Quality**: Adjust `GetJpegQuality()` [line 343] formula or seed-derived `jpegQuality` [line 72]
- **Corruption modes**: Edit `DamageJpegBytes()` [line 401] mode selection (0=bit flips, 1=byte insertion, 2=byte deletion)

### Task: Optimize Performance
- Profile `Render()` loop; common hotspots:
  - Surface allocations → pre-allocate or use stackalloc
  - Repeated hash calls → cache intermediate results
  - Nested pixel loops → consider block-based processing
- **Avoid**: String concatenation, LINQ in render loop, `Math.Pow()` with non-compile-time exponents

---

## Testing & Validation

### Determinism Check
```
1. Render with Seed=1, all effects enabled, Stack=true
2. Save output as reference
3. Re-render with identical settings
4. Pixel-perfect match required (no tolerance)
```
**If fails**: Check for global random state, thread-unsafe Surface access, or coordinate-dependent heap allocations.

### Visual QA
- Test each effect independently (enable only one checkbox)
- Verify alpha channel preserved (enable on semi-transparent source)
- Confirm no edge artifacts (render with `Rect` that covers image edges)

### CodeLab Integration
- Load in Paint.NET CodeLab
- Test on different image sizes (256×256, 1920×1080, etc.)
- Verify UI controls respond and update preview in real-time

---

## Performance Constraints

| Constraint | Implication | Rationale |
|-----------|------------|-----------|
| No heap allocs in render loop | Use `stackalloc` or pre-allocated surfaces | Paint.NET may render large images; GC pressure causes stuttering |
| Clamp all pixel access | `SampleClamped()` helper | Out-of-bounds reads cause crashes or undefined behavior |
| Deterministic hashing only | No `System.Random` or global state | Threaded tile rendering must produce identical results |
| Single-pass JPEG (optional) | Pre-compute `jpegSurface` once before loop | JPEG encode/decode is ~50ms per 1920×1080; repeated calls kill performance |
| Avoid trigonometry in inner loop | Pre-compute `Math.Sin()` tables or use `GetMaskValue()` cached approach | `Math.Sin()` is slow; Perlin-like noise is more efficient |

---

## Debugging Tips

- **Seed reproducibility fails**: Check `Hash()` for uninitialized state or integer overflow bugs. Use `unchecked` blocks for intentional wraps.
- **Alpha channel lost**: Verify all effects use `BlendPreserveAlpha()` or explicitly set `alpha = src[x,y].A`.
- **JPEG codec not found**: `GetJpegCodec()` [line 496] returns `null` on systems without built-in JPEG support; fallback to copy source.
- **Edge artifacts**: Ensure `SampleClamped()` is used; check Rectangle bounds in `Render()` loop.
- **Effect ordering issues**: Trace `GetEffectOrder()` Fisher-Yates shuffle; verify `effectOrder[i]` matches `ApplyEffectById()` conditions.

---

## Documentation & References

- **Paint.NET CodeLab API**: Refer to Paint.NET CodeLab documentation for `Surface`, `ColorBgra`, `Rectangle` classes
- **Project specification**: [GLITCH_EFFECT_PLUGIN_PLAN.md](GLITCH_EFFECT_PLUGIN_PLAN.md) — design decisions, rationale, and feature roadmap
- **Implementation progress**: [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) — track milestones and open tasks

---

## Next Steps for Agents

When asked to **modify**, **extend**, or **debug** this effect:

1. **Reference the design**: Effects must preserve determinism and alpha
2. **Use coordinate-based hashing**: Never introduce mutable state in render loops
3. **Test determinism**: Render same seed twice; outputs must be pixel-perfect
4. **Profile before optimizing**: Measure the actual bottleneck (JPEG codec, hash calls, or image I/O)
5. **Link to existing patterns**: New effects should follow the `BlendPreserveAlpha()` + `ApplyEffectById()` dispatcher pattern

---

*Last updated: 2026-06-22*  
*Project scope: Paint.NET CodeLab effect exemplar for deterministic glitch rendering*
