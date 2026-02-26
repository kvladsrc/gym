#include <benchmark/benchmark.h>

#include <algorithm>
#include <numeric>
#include <vector>

#include "cpp/warmup/quick_sort.hpp"

static std::vector<int> make_reversed(int n) {
  std::vector<int> v(n);
  std::iota(v.rbegin(), v.rend(), 0);
  return v;
}

static void BM_QuickSort(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    const int n = static_cast<int>(v.size());
    warmup::quick_sort(v, 0, n - 1);
    benchmark::DoNotOptimize(v);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
// Reversed input: worst case for naive pivot selection.
BENCHMARK(BM_QuickSort)->Range(8, 1 << 16);

static void BM_StdSort_ForQuick(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    std::sort(v.begin(), v.end());
    benchmark::DoNotOptimize(v);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_StdSort_ForQuick)->Range(8, 1 << 16);
