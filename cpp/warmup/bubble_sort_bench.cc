#include <benchmark/benchmark.h>

#include <algorithm>
#include <numeric>
#include <vector>

#include "cpp/warmup/bubble_sort.hpp"

static std::vector<int> make_reversed(int n) {
  std::vector<int> v(n);
  std::iota(v.rbegin(), v.rend(), 0);
  return v;
}

static void BM_BubbleSort(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    warmup::bubble_sort(v);
    benchmark::DoNotOptimize(v);
  }
}
// Bubble sort is O(n²) — keep N small.
BENCHMARK(BM_BubbleSort)->Range(8, 1 << 13);

static void BM_StdSort_ForBubble(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    std::sort(v.begin(), v.end());
    benchmark::DoNotOptimize(v);
  }
}
BENCHMARK(BM_StdSort_ForBubble)->Range(8, 1 << 13);
