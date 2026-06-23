# Performance Testing Plan — Phase 1 Optimizations

**Objective**: Measure performance improvements from Phase 1 optimizations (byte array pooling + codec caching)

**Expected Gains**: 20–30% overall speedup when testing FloatingPointError + PNG/GIF effects

---

## 1. Instrumentation Strategy

### 1.1 Timing Points (Add via CodeLab comments/logs)

**Critical timings to capture**:
- **Total Render Time**: Start of `Render()` → end of tile copy to `dst`
- **Effect Pass Times**: Per-effect duration in `ApplyEffectPass()` loop
- **Codec Operations**: `PrepareJpegSurfaces()`, `CreatePngBinaryDamageSurface()`, `CreateGifBinaryDamageSurface()`
- **Allocation Overhead**: (Estimated via effect complexity and memory profiling)

**Measurement approach**: 
- Use `System.Diagnostics.Stopwatch` for sub-millisecond precision
- Capture timings in a dictionary keyed by effect ID + iteration
- Log aggregated results to Paint.NET debug console or external file

---

## 2. Test Scenarios

### Scenario A: FloatingPointError Intensity Test
**Goal**: Measure byte array pooling elimination impact

| Test Case | Image Size | Effect Mix | Intensity | Iterations | Expected Duration |
|-----------|------------|-----------|-----------|------------|------------------|
| A1 | 512×512 | FloatingPointError only | 0.8 | 5 | 50–100ms baseline |
| A2 | 1024×1024 | FloatingPointError only | 0.8 | 5 | 200–400ms baseline |
| A3 | 1920×1080 | FloatingPointError only | 0.8 | 3 | 800–1200ms baseline |

**Measurement**: Average frame time (ms), per-pixel allocation overhead (estimated)

**Success Criteria**: 
- A1 should improve by ~15–20% (byte array eliminations)
- A2, A3 should scale linearly (no quadratic memory allocation penalty)

---

### Scenario B: PNG/GIF Codec Caching Test
**Goal**: Measure codec surface reuse impact

| Test Case | Image Size | Effect Mix | Seed Variation | Iterations | Expected Duration |
|-----------|------------|-----------|---------------|-----------| --------|
| B1 | 512×512 | PNG only | Fixed seed (1) | 5 | 30–60ms baseline |
| B2 | 512×512 | GIF only | Fixed seed (1) | 5 | 30–60ms baseline |
| B3 | 512×512 | PNG + GIF | Fixed seed (1) | 5 | 60–120ms baseline |
| B4 | 1024×1024 | PNG + GIF | Fixed seed (1) | 5 | 200–400ms baseline |

**Measurement**: Frame time per effect, cache hit rate (if logged), surface allocation count

**Success Criteria**:
- B1, B2: First render establishes cache; subsequent renders reuse cached surface
- B3: PNG + GIF with shared cache should be ~1.5–1.7× cost of single effect (not 2×)
- B4: Large image should benefit from 30–50ms savings when both PNG+GIF enabled

---

### Scenario C: Mixed Effect Stack Test (All 10 Effects)
**Goal**: Measure overall performance with full effect pipeline

| Test Case | Image Size | All Effects Enabled? | Stack Mode | Seed | Iterations | Expected Duration |
|-----------|------------|----|----|---|--|-----|
| C1 | 512×512 | Yes | true | 1 | 3 | 500–800ms baseline |
| C2 | 1024×1024 | Yes | true | 1 | 2 | 1500–2500ms baseline |
| C3 | 512×512 | Yes | true | 1,2,3 | 9 (3 seeds × 3 iter) | 1500–2500ms cumulative |

**Measurement**: Total frame time, per-effect contribution, cache invalidation frequency

**Success Criteria**:
- C1: 20–30% improvement from Phase 1 optimizations
- C2: Scaling should remain near-linear (no pathological allocation behavior)
- C3: Cache should not carry over between seeds (verify cache invalidation)

---

### Scenario D: Determinism Verification (Sanity Check)
**Goal**: Ensure optimizations preserve determinism

| Test Case | Image Size | Effect Config | Seed | Iteration Runs | Expected Result |
|-----------|------------|--|---|---|--|
| D1 | 512×512 | FloatingPointError only | 1 | 2 | Pixel-perfect match |
| D2 | 1024×1024 | PNG + GIF | 1 | 2 | Pixel-perfect match |
| D3 | 512×512 | All effects | 1 | 2 | Pixel-perfect match |

**Measurement**: Binary pixel-by-pixel comparison (CRC or diff)

**Success Criteria**: All pixel values identical between two renders with same seed + settings

---

## 3. Measurement Methodology

### 3.1 Baseline Collection (Pre-Optimization)
1. Create backup branch of code without Phase 1 changes
2. Render each test scenario, capture timings
3. Log: total frame time, allocations, GC pressure (if available)
4. Average across multiple iterations

### 3.2 Optimized Collection (Post-Phase 1)
1. Use current code with Phase 1 changes
2. Render identical test scenarios
3. Capture same metrics
4. Average across iterations

### 3.3 Comparison Calculation
```
Improvement % = ((Baseline Time - Optimized Time) / Baseline Time) × 100

Per-scenario deltas:
- Scenario A (FloatingPointError): Expected 15–20% speedup
- Scenario B (PNG/GIF caching): Expected 20–40% speedup for multi-codec
- Scenario C (Full stack): Expected 15–25% speedup
- Scenario D (Determinism): 0% timing change (binary verification only)
```

---

## 4. Data Collection Points

### In-Code Instrumentation Locations

**Optimization 1.1 (Byte Array Pooling)**
- **Location**: `FloatingPointError()` function
- **Measure**: Per-pixel execution time, allocation count before/after
- **Indicator**: If allocation count drops from 1M to 0 per 1K×1K image, pooling succeeded

**Optimization 1.2 (PNG/GIF Surface Caching)**
- **Location**: `ApplyEffectPass()` for effectId 7, 8
- **Measure**: 
  - First call: full decode time (baseline)
  - Subsequent calls: cache reuse time (should be ~1–2ms for Surface copy)
  - Cache invalidation frequency (should only reset on seed change)
- **Indicator**: If subsequent PNG/GIF calls are 20–30× faster, caching works

### Metrics to Log

| Metric | Format | Purpose |
|--------|--------|---------|
| `frame_time_ms` | float | Total render time per frame |
| `effect_time_ms[effectId]` | float[] | Per-effect duration |
| `cache_hit_count` | int | PNG/GIF surface cache reuses |
| `cache_miss_count` | int | PNG/GIF surface recomputes |
| `allocation_count` | int | Estimated per-pixel allocations (FloatingPointError) |
| `seed_value` | int | Seed used for test |
| `image_dimensions` | "WxH" | Test image size |

---

## 5. Test Execution Steps

### Phase 1: Baseline (Without Optimizations)

```
1. Checkout original code (or create backup)
2. For each scenario (A1, A2, A3, B1–B4, C1–C3):
   - Render 5–10 iterations with Stopwatch
   - Capture metrics to log file
   - Record average, min, max times
3. Analyze allocation overhead
```

### Phase 2: Optimized (With Phase 1)

```
1. Use current optimized code
2. For each scenario (A1, A2, A3, B1–B4, C1–C3):
   - Render 5–10 iterations with Stopwatch
   - Capture metrics to log file
   - Record average, min, max times
3. Verify determinism (Scenario D)
```

### Phase 3: Analysis & Reporting

```
1. Calculate improvement % for each scenario
2. Compare allocation counts
3. Verify cache hit/miss ratios
4. Validate determinism across all D-series tests
5. Document findings in PERFORMANCE_RESULTS.md
```

---

## 6. Success Criteria

| Criterion | Target | Rationale |
|-----------|--------|-----------|
| **A1–A3 Improvement** | 15–20% faster | Byte array pooling eliminates per-pixel allocation |
| **B3 Scaling** | ~1.5–1.7× single effect cost | Codec cache reuse prevents redundant encode/decode |
| **B4 Absolute Time** | 30–50ms savings | Cache hit on second PNG/GIF call |
| **C1–C3 Improvement** | 20–30% faster | Combined effect of pooling + caching across full stack |
| **C2 Scaling** | Linear or sub-quadratic | No allocation pathology on large images |
| **D1–D3 Determinism** | 100% pixel match | Optimizations preserve numerical accuracy |
| **Cache Invalidation** | Only on seed change | Verify `cachedSeedForCodecs` logic |

---

## 7. Tools & Logging

### Timing Capture
- **Language**: C# `System.Diagnostics.Stopwatch`
- **Precision**: Microseconds (`.ElapsedMilliseconds` or `.Elapsed.TotalMilliseconds`)
- **Output**: Log file or Paint.NET debug console

### Optional Profiling Tools
- Windows Performance Toolkit (ETW): CPU time, allocation count, GC pressure
- Paint.NET memory profiler: Heap snapshots before/after
- Custom CSV log: Import timings into Excel for charting

### Comparison Tools
- Image diff: Use Paint.NET's built-in tools or external `ImageMagick compare`
- Statistical analysis: Python script to aggregate, calculate percentiles, plot trends

---

## 8. Expected Results Summary

| Phase | Scenario | Baseline (ms) | Optimized (ms) | Improvement |
|-------|----------|---|---|---|
| 1 | A1 (FPE, 512×512) | 75 | 61 | **19%** |
| 1 | A2 (FPE, 1K×1K) | 300 | 245 | **18%** |
| 1 | B3 (PNG+GIF, 512×512, cached) | 100 | 75 | **25%** |
| 1 | B4 (PNG+GIF, 1K×1K, cached) | 300 | 260 | **13%** |
| 1 | C1 (All 10, 512×512) | 650 | 510 | **21%** |
| 1 | C2 (All 10, 1K×1K) | 2000 | 1550 | **22%** |
| Determinism | D1–D3 | N/A | N/A | **Pixel-perfect** |

---

## 9. Reporting Template

After execution, populate:

```markdown
# Performance Test Results — Phase 1

**Date**: [Date]
**Optimizations Tested**: Byte array pooling, PNG/GIF codec caching
**Test Environment**: Paint.NET [version], CodeLab, Windows [version]

## Summary
- Average improvement: **X%**
- Best case improvement: **Y%** (Scenario B3)
- Worst case improvement: **Z%** (Scenario C2)
- Determinism: ✅ All tests pixel-perfect match

## Detailed Results
[Insert table from Section 8 with actual measurements]

## Metrics
- Total frames rendered: [count]
- Average cache hit ratio: [X%]
- Allocation elimination: [Y% reduction]

## Blockers/Issues
[Any unexpected behavior]
```

---

## 10. Next Steps (Phase 2 Optimization Planning)

After Phase 1 results, if additional speedup is desired:
- **Phase 2 candidates**: Selective region processing, hash memoization, tile-aware rendering
- **Expected additional**: 30–50% speedup
- **Measurement**: Reuse same test scenarios; compare Phase 1 optimized → Phase 2 optimized

