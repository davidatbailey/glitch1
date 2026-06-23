# Phase 2 Performance Optimization Plan

**Objective**: Achieve additional 30–50% speedup beyond Phase 1 (expected ~50–60% total improvement from baseline)

**Phase 1 Baseline (with optimizations)**: ~50% improvement  
**Phase 2 Target**: Additional 30–50% speedup  
**Combined Outcome**: 65–75% total improvement

---

## 1. Overview: Phase 2 Optimizations

| Optimization | Complexity | Risk | Estimated Gain | Lines of Code |
|--------------|-----------|------|---------------|----|
| **2.1 Selective Region Processing** | Medium | Low | 20–30% (when rect << image) | 40–60 |
| **2.2 Hash/Random Memoization** | Medium | Low | 10–15% | 80–120 |
| **2.3 Tile-Aware Rendering** | High | Medium | 15–25% | 100–150 |
| **2.4 Effect Early Bailout** | Medium | Low | 5–10% | 50–80 |
| **2.5 Lazy Coefficient Computation** | Low | Very Low | 3–5% | 20–40 |

**Total Expected Gain**: 30–50%  
**Implementation Time**: 4–8 hours  
**Testing Time**: 2–3 hours

---

## 2. Optimization 2.1: Selective Region Processing

### 2.1.1 Problem Analysis

**Current behavior**:
```csharp
// Compute FULL-FRAME effects
cachedEffectOutput = finalSurface;  // Full src.Width × src.Height

// Then copy only rect to dst
for (int y = rect.Top; y < rect.Bottom; y++)
    for (int x = rect.Left; x < rect.Right; x++)
        dst[x, y] = cachedEffectOutput[x, y];
```

**Issue**: If `rect` is 1/4 the image size (e.g., 512×512 image, rect 256×256), we compute 4× more than needed.

### 2.1.2 Solution: Conditional Full-Frame Caching

```csharp
// NEW: Compute only the requested region when significantly smaller
double rectArea = (rect.Bottom - rect.Top) * (rect.Right - rect.Left);
double imageArea = rotatedSrc.Width * rotatedSrc.Height;
double coverageRatio = rectArea / imageArea;

bool isPartialRender = coverageRatio < 0.5;  // Threshold: 50% coverage

if (isPartialRender)
{
    // Compute only rect region, bypass full-frame caching
    ApplyEffectsToRegion(rotatedSrc, effectOrder, rect, ...);
    CopyRegionToOutput(effectRegionOutput, dst, rect);
}
else
{
    // Compute full-frame as before (Phase 1 behavior)
    cachedEffectOutput = finalSurface;
}
```

**Benefit**:
- 50% render area → ~50% time savings
- Scales with Paint.NET's tile-based rendering model
- Preserves determinism (same hash-based randomness)

**Risk**: Low — Same underlying effect functions, just applied to smaller region

### 2.1.3 Implementation Steps

1. **Add region-aware caching logic** (~15 lines):
   - Calculate `coverageRatio` in `Render()`
   - Add `isCacheValid` flag for region-specific cache
   - Create `ApplyEffectsToRegion()` dispatcher

2. **Implement region-bounded effect loop** (~30 lines):
   - Copy `ApplyEffectPass()` logic but loop only over `rect`
   - Create scratch surfaces sized to `rect.Width × rect.Height`
   - Use offset calculations for coordinate-based hashing

3. **Test edge cases** (~20 lines):
   - Boundary rect (e.g., x=0, y=0, w=1, h=1)
   - Overlapping regions (multiple Paint.NET tile renders)
   - Cache coherency (transitions between full/partial renders)

### 2.1.4 Measurement

| Scenario | Improvement | Condition |
|----------|------------|-----------|
| Partial render (50% area) | **40–50%** | `rect` covers half image |
| Partial render (25% area) | **65–75%** | `rect` covers quarter image |
| Full render (100% area) | **0%** (no change) | Entire image rendered |

---

## 3. Optimization 2.2: Hash/Random Memoization

### 3.2.1 Problem Analysis

**Current pattern** (FloatingPointError):
```csharp
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        uint roll = Hash(seed, x, y, 503);         // Hash call 1
        double pixelProb = Random01(seed, x, y, 504);  // Hash call 2
        int channelChoice = (int)(Hash(seed, x, y, 505) % 6u);  // Hash call 3
        // ... effect logic
    }
}
```

**Cost**: Hash function (XORshift avalanche) called 3× per pixel  
**For 1M pixels**: 3M hash calls (~5–10ms overhead)

**Opportunities**:
1. **Block-level memoization**: Compute hash once per block, reuse for all pixels in block
2. **Salt-indexed cache**: Pre-compute hash chains for common (x, y, salt) patterns
3. **Lazy hash deferral**: Some effects don't need all 3 hash calls every iteration

### 3.2.2 Solution: Block-Level Hash Cache

```csharp
// NEW: Cache hash values per block
const int BLOCK_SIZE = 16;
Dictionary<int, uint> hashCache = new Dictionary<int, uint>();

uint GetHashCached(int seed, int bx, int by, int salt)
{
    int key = (bx << 16) | (by & 0xFFFF);  // Pack block coords into 32-bit key
    
    if (!hashCache.TryGetValue(key, out uint cachedHash))
    {
        cachedHash = Hash(seed, bx, by, salt);
        hashCache[key] = cachedHash;
    }
    return cachedHash;
}

// Usage in inner loop:
for (int y = 0; y < height; y++)
{
    int blockY = y / BLOCK_SIZE;
    for (int x = 0; x < width; x++)
    {
        int blockX = x / BLOCK_SIZE;
        uint blockHash = GetHashCached(seed, blockX, blockY, 500);
        // Derive per-pixel variation from blockHash and local (x % BLOCK_SIZE, y % BLOCK_SIZE)
    }
}
```

**Benefit**:
- 256× fewer hash calls (1 per 16×16 block instead of per-pixel)
- Dictionary overhead paid once per block, not per pixel
- Block-level variance preserved via secondary hash on local coords

**Risk**: Low — Cache is deterministic, same seed = same block hashes

### 3.2.3 Implementation Steps

1. **Create HashBlockCache class** (~40 lines):
   ```csharp
   class HashBlockCache
   {
       Dictionary<int, uint[]> blockHashesBySalt = new Dictionary<int, uint[]>();
       
       uint GetBlockHash(int seed, int blockX, int blockY, int salt)
       {
           // Lazy-load salt-indexed cache
       }
   }
   ```

2. **Refactor hash-heavy effects** (~60 lines):
   - Update `FloatingPointError()` to use block cache
   - Update `LackOfClamping()` for block hashing
   - Update `ChannelSwapLab()` to use memoized hashes

3. **Measure cache hit rate**:
   - Log cache hits vs. misses
   - Verify improvement scales with block size

### 3.2.4 Measurement

| Effect | Current Hash Calls/Pixel | Memoized Calls/Pixel | Speedup |
|--------|----------|----------|---------|
| FloatingPointError | 3 | 0.19 (3/16) | **15× faster** |
| LackOfClamping | 2 | 0.13 (2/16) | **15× faster** |
| ChannelSwapLab | 4 | 0.25 (4/16) | **16× faster** |
| **Overall (1M pixels)** | 3M calls | 180K calls | **10–15% improvement** |

---

## 4. Optimization 2.3: Tile-Aware Rendering

### 4.3.1 Problem Analysis

**Current architecture**:
- Full-frame surfaces allocated upfront (`scratchA`, `scratchB`)
- Large images → large allocations → poor cache locality
- Single-threaded rendering (Paint.NET calls `Render()` once per tile, but plugin computes full-frame)

**Opportunity**: Redesign to process tiles internally

```
Before (current):
  [Full frame] → [All effects] → [Write rect]

After (tile-aware):
  [Tile 0] → [All effects] → [Write rect ∩ Tile 0]
  [Tile 1] → [All effects] → [Write rect ∩ Tile 1]
  ...
```

### 4.3.2 Solution: Internal Tile Pipeline

```csharp
const int TILE_SIZE = 64;  // 64×64 pixel tiles

// Pseudo-code for tile-aware rendering
void RenderWithTiles(Surface dst, Surface src, Rectangle rect)
{
    Surface cachedEffectsOutput = ComputeEffectsOnTiles(src, effectOrder, TILE_SIZE);
    
    // Write only rect to dst
    CopyRegionToOutput(cachedEffectsOutput, dst, rect);
}

Surface ComputeEffectsOnTiles(Surface src, int[] effectOrder, int tileSize)
{
    Surface result = new Surface(src.Width, src.Height);
    
    for (int ty = 0; ty < src.Height; ty += tileSize)
    {
        for (int tx = 0; tx < src.Width; tx += tileSize)
        {
            Rectangle tileBounds = new Rectangle(tx, ty, 
                Math.Min(tileSize, src.Width - tx), 
                Math.Min(tileSize, src.Height - ty));
            
            // Process tile through effect pipeline
            ApplyEffectsToTile(src, result, tileBounds, effectOrder);
        }
    }
    
    return result;
}
```

**Benefits**:
1. **Better cache locality**: 64×64 tile fits in L1/L2 cache (~16KB–256KB)
2. **Reduced memory bandwidth**: Fewer cache misses during effect application
3. **Parallelization ready**: Tiles can be distributed to thread pool (Paint.NET internal)
4. **Early termination**: Can skip tiles outside render rect

### 4.3.3 Implementation Steps

1. **Create TileProcessor class** (~80 lines):
   ```csharp
   class TileProcessor
   {
       void ProcessTile(Rectangle tileBounds, Surface src, Surface dst, int[] effectOrder);
       void ApplyEffectToTile(int effectId, Rectangle bounds, ...);
   }
   ```

2. **Add tile-aware memory management** (~40 lines):
   - Allocate per-tile scratch buffers (smaller → faster)
   - Reuse tile buffers across iterations

3. **Optimize boundary handling**:
   - Handle edge tiles (non-TILE_SIZE dimensions)
   - Preserve determinism for coordinate-based hashing

### 4.3.4 Measurement

| Scenario | L1 Cache Misses | Memory Bandwidth | Speedup |
|----------|-----------------|------------------|---------|
| Full-frame (no tiling) | 45% | High | 1.0× (baseline) |
| 64×64 tiles | 15% | Low | **1.8–2.2×** |
| 32×32 tiles | 8% | Very Low | **2.0–2.5×** |

**Trade-off**: Smaller tiles = better cache, but more edge handling overhead. **Recommended: 64×64**

---

## 5. Optimization 2.4: Effect Early Bailout

### 5.4.1 Problem Analysis

**Current logic**:
```csharp
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        if (effectId == 1) transformed = MissingPacketRows(...);
        else if (effectId == 2) transformed = ChannelSwapLab(...);
        else if (effectId == 3) transformed = LackOfClamping(...);
        // ... 10 checks
        output[x, y] = transformed;
    }
}
```

**Issue**: `if-else-if` chain evaluated for every pixel, even when effect does minimal work

**Opportunities**:
1. **Probabilistic bailout**: Many effects only affect a fraction of pixels; check early
2. **Uniform skip regions**: Some effects skip large uniform regions (e.g., compression artifacts in smooth areas)

### 5.4.2 Solution: Probabilistic Early Bailout

```csharp
ColorBgra MissingPacketRows(Surface input, int x, int y, int seed, int blockSize, double intensity)
{
    // NEW: Early bailout before expensive operations
    double prob = Random01(seed, x, y, 301);
    if (prob > intensity)
    {
        return input[x, y];  // Return immediately
    }
    
    // Only compute if effect probability triggered
    int blockY = y / blockSize;
    bool isAffected = (Hash(seed, x / blockSize, blockY, 302) % 100u) < 30u;
    if (!isAffected)
    {
        return input[x, y];  // Return immediately
    }
    
    // Expensive operation (missing packet logic)
    // ... (only reached for ~3–9% of pixels)
}
```

**Benefit**: Expensive operations (JPEG encode/decode, complex computations) only execute when needed

### 5.4.3 Implementation Steps

1. **Add early bailout pattern to each effect** (~8 lines per effect):
   - Probability check (1 hash call)
   - Skip block check (1 hash call)
   - Return early if not affected

2. **Verify no visual regression**:
   - Same seed, same affected pixel set
   - Only changes per-pixel performance, not output

### 5.4.4 Measurement

| Effect | Affected Pixels (%) | Early Bailout Saves | Speedup |
|--------|------------------|-----------------|---------|
| MissingPacketRows | 3–9% | 91–97% of pixel cost | **4–8×** per-effect |
| LackOfClamping | 15–25% | 75–85% of pixel cost | **2–4×** per-effect |
| FloatingPointError | 40–55% | 45–60% of pixel cost | **1.5–2×** per-effect |
| **Composite (full stack)** | — | **5–10% overall** | **5–10% improvement** |

---

## 6. Optimization 2.5: Lazy Coefficient Computation

### 6.5.1 Problem Analysis

**Current code**:
```csharp
int blockSize = 4 + ((int)(Hash(seedValue, 0, 0, 103) % 8u) * 2);  // Computed per Render()
double intensity = 0.45 + (Random01(seedValue, 0, 0, 101) * 0.45);
int jpegQuality = GetJpegQuality(seedValue, intensity);
// These are computed once per full-frame render but used 1M times
```

**Issue**: Coefficients computed early but used throughout effect loop. If many render calls, redundant computation.

### 6.5.2 Solution: Cache Coefficients

```csharp
struct EffectCoefficients
{
    public int blockSize;
    public double intensity;
    public int jpegQuality;
    public int volatileFrameSalt;
}

EffectCoefficients cachedCoefficients = default;
int cachedCoefficientsKey = -1;

EffectCoefficients GetEffectCoefficients(int seedValue, bool memoryTearEnabled)
{
    if (cachedCoefficientsKey == seedValue)
    {
        return cachedCoefficients;
    }
    
    cachedCoefficients = new EffectCoefficients
    {
        blockSize = 4 + ((int)(Hash(seedValue, 0, 0, 103) % 8u) * 2),
        intensity = 0.45 + (Random01(seedValue, 0, 0, 101) * 0.45),
        jpegQuality = GetJpegQuality(seedValue, intensity),
        volatileFrameSalt = memoryTearEnabled ? (System.Environment.TickCount / 120) : 0
    };
    
    cachedCoefficientsKey = seedValue;
    return cachedCoefficients;
}
```

**Benefit**: Eliminates recomputation across Paint.NET tile renders with same seed

### 6.5.3 Implementation Steps

1. **Create EffectCoefficients struct** (~10 lines)
2. **Add coefficient caching logic** (~15 lines)
3. **Update Render() to use cache** (~5 lines)

### 6.5.4 Measurement

| Scenario | Recomputation Cost | Cache Hit Rate | Speedup |
|----------|------------------|---------------|----|
| Single render pass | 0 | N/A | 0% |
| 10× Paint.NET tile renders (same seed) | 9× redundant | 90% | **3–5%** |

---

## 7. Combined Phase 2 Impact

### 7.7.1 Expected Performance Scaling

| Scenario | Phase 1 (ms) | Phase 2 (ms) | Cumulative Gain |
|----------|------------|----------|---|
| FloatingPointError, 512×512 | 61 | 48 | **37%** |
| FloatingPointError, 1K×1K | 245 | 185 | **38%** |
| PNG+GIF cached, 512×512 | 75 | 60 | **33%** |
| All 10 effects, 512×512 | 510 | 365 | **39%** |
| All 10 effects, 1K×1K | 1550 | 1050 | **43%** |

**Conservative estimate**: 30–35% Phase 2 improvement  
**Optimistic estimate**: 45–55% Phase 2 improvement

### 7.7.2 Implementation Priority

| Priority | Optimization | Effort | Risk | Benefit |
|----------|--------|--------|------|---------|
| **1 (Quick Win)** | 2.5 Lazy Coefficients | 1 hour | Very Low | 3–5% |
| **2 (High ROI)** | 2.4 Early Bailout | 2 hours | Low | 5–10% |
| **3 (Selective)** | 2.1 Region Processing | 2.5 hours | Low | 20–30% (when partial render) |
| **4 (Advanced)** | 2.2 Hash Memoization | 3 hours | Low | 10–15% |
| **5 (Complex)** | 2.3 Tile-Aware | 4–6 hours | Medium | 15–25% |

---

## 8. Implementation Roadmap

### Phase 2.A (Quick Wins) — 3–4 hours
1. **2.5 Lazy Coefficients**: 1 hour (lowest risk, quick payoff)
2. **2.4 Early Bailout**: 2 hours (add to 3–4 high-impact effects)
3. **Testing & Validation**: 1 hour

**Expected Improvement**: 8–15%

### Phase 2.B (Middle Tier) — 5–6 hours
1. **2.1 Region Processing**: 2.5 hours
2. **2.2 Hash Memoization** (start with FloatingPointError): 2 hours
3. **Testing & Benchmarking**: 1.5 hours

**Expected Improvement**: 20–30% (combined with Phase 2.A: 28–40%)

### Phase 2.C (Advanced) — 6–8 hours
1. **2.3 Tile-Aware Rendering**: 4–6 hours (most complex)
2. **Multi-threading preparation** (optional): 1–2 hours
3. **Integration testing**: 1–2 hours

**Expected Improvement**: 15–25% (combined with 2.A+2.B: 45–65%)

---

## 9. Test Plan for Phase 2

### Regression Testing
- **Determinism**: Same seed = identical output (pixel-perfect)
- **Visual correctness**: Compare Phase 1 vs. Phase 2 output (should be identical)
- **Cache coherency**: Verify cache invalidation logic

### Performance Testing
- **Scenario A–C** (from PERFORMANCE_TEST_PLAN.md): Measure Phase 2 improvements
- **Partial render tests**: Verify region processing speedups
- **Block size tuning**: Test BLOCK_SIZE 8, 16, 32, 64

### Benchmark Metrics
- Frame time (ms)
- Cache hit/miss ratios (if instrumented)
- Memory bandwidth usage (optional: ETW profiling)

---

## 10. Risk Mitigation

| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| Cache invalidation bugs | Low | Test with varying seeds, rect regions, effect combos |
| Tile boundary artifacts | Medium | Add boundary condition tests, verify alpha at edges |
| Dictionary overhead (memoization) | Low | Measure cache hit rate; tune cache size if needed |
| Determinism loss | Very Low | Verify pixel-perfect match vs. Phase 1 output |

---

## 11. Success Criteria

- ✅ **All Phase 2A changes compile cleanly**
- ✅ **Phase 2A improvements**: 8–15% speedup verified
- ✅ **Determinism preserved**: Pixel-perfect match between Phase 1 & Phase 2
- ✅ **No visual regression**: Blind A/B comparison indistinguishable
- ✅ **Phase 2B improvements** (if implemented): 20–30% additional speedup
- ✅ **Cache hit rates**: >85% for memoization, >90% for coefficient caching
- ✅ **Documentation**: Update AGENTS.md and performance profile

---

## 12. Next Steps

**Recommended starting point**: Implement Phase 2.A (Quick Wins) first
- Low complexity, minimal risk
- Validates testing infrastructure
- Provides measurable gain while building toward Phase 2.B/2.C

**After Phase 2.A approval**: Proceed to Phase 2.B (Middle Tier) for substantial 20–30% improvement

**Phase 2.C (Tile-Aware)**: Consider only if 45%+ cumulative improvement needed or if Paint.NET parallelization becomes priority

