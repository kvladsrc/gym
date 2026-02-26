#include <benchmark/benchmark.h>

#include <algorithm>
#include <numeric>
#include <vector>

#include "cpp/warmup/merge_sort.hpp"

static std::vector<int> make_reversed(int n) {
  std::vector<int> v(n);
  std::iota(v.rbegin(), v.rend(), 0);
  return v;
}

static void BM_MergeSort(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    warmup::merge_sort(v);
    benchmark::DoNotOptimize(v);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_MergeSort)->Range(8, 1 << 20);

static void BM_StdSort(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    auto v = make_reversed(static_cast<int>(state.range(0)));
    state.ResumeTiming();
    std::sort(v.begin(), v.end());
    benchmark::DoNotOptimize(v);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_StdSort)->Range(8, 1 << 20);
