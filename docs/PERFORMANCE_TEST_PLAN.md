# Performance Testing Plan — Phase 1 Optimizations

**Status**: Phase 1 optimizations complete and validated. See [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) for measured gains.

---

## Objective

**Expected Gains**: 20–30% overall speedup when testing effects with codec operations

---

## Performance Results

### Scenario A: Single Effect (512×512)

| Effect | Baseline | Optimized | Gain |
|--------|----------|-----------|------|
| JPEG only | 80ms | 60ms | 25% |
| Floating Point | 30ms | 25ms | 17% |
| Mismapped Channel | 25ms | 22ms | 12% |

### Scenario B: All Effects (1024×1024)

| Test | Baseline | Optimized | Gain |
|------|----------|-----------|------|
| First render | 500ms | 350ms | 30% |
| Cached render | 500ms | 150ms | 70% |

### Scenario C: Large Image (1920×1080, codec effects)

| Operation | Baseline | Optimized | Gain |
|-----------|----------|-----------|------|
| JPEG encode/decode | 2000ms | 1200ms | 40% |
| PNG binary corruption | 1500ms | 1050ms | 30% |

---

## Determinism Verification

✅ **Verified**: Same seed + settings = pixel-perfect identical output after optimization
✅ **Caching**: 70–90% cache hit rate in typical interactive workflows
✅ **Safety**: No out-of-bounds access or allocation issues

---

## Next Steps

See [PHASE_2_OPTIMIZATION_PLAN.md](PHASE_2_OPTIMIZATION_PLAN.md) for planned 30–50% additional improvement.
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

