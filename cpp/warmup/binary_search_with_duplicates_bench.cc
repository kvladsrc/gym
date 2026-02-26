#include <benchmark/benchmark.h>

#include <algorithm>
#include <numeric>
#include <vector>

#include "cpp/warmup/binary_search_with_duplicates.hpp"

static std::vector<int> make_sorted(int n) {
  std::vector<int> v(n);
  std::iota(v.begin(), v.end(), 0);
  return v;
}

// Search hit: element in the middle.
static void BM_LowerBound_Hit(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto v = make_sorted(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::lower_bound(v, n / 2));
  }
}
BENCHMARK(BM_LowerBound_Hit)->Range(8, 1 << 20);

// Search miss: element past the end.
static void BM_LowerBound_Miss(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto v = make_sorted(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::lower_bound(v, n + 1));
  }
}
BENCHMARK(BM_LowerBound_Miss)->Range(8, 1 << 20);

static void BM_StdLowerBound_Hit(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto v = make_sorted(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(std::lower_bound(v.begin(), v.end(), n / 2));
  }
}
BENCHMARK(BM_StdLowerBound_Hit)->Range(8, 1 << 20);

static void BM_StdLowerBound_Miss(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto v = make_sorted(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(std::lower_bound(v.begin(), v.end(), n + 1));
  }
}
BENCHMARK(BM_StdLowerBound_Miss)->Range(8, 1 << 20);
