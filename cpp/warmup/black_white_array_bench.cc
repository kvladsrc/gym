#include <benchmark/benchmark.h>

#include <set>

#include "cpp/warmup/black_white_array.hpp"

static void BM_InsertSequential(benchmark::State& state) {
  for (auto _ : state) {
    bwarray arr;
    for (int i = 0; i < state.range(0); ++i) {
      arr.insert(i);
    }
    benchmark::DoNotOptimize(arr);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_InsertSequential)->Range(8, 1 << 20);

static void BM_SearchHit(benchmark::State& state) {
  bwarray arr;
  const int n = static_cast<int>(state.range(0));
  for (int i = 0; i < n; ++i) arr.insert(i);
  for (auto _ : state) {
    benchmark::DoNotOptimize(arr.search(n / 2));
  }
}
BENCHMARK(BM_SearchHit)->Range(8, 1 << 20);

static void BM_SearchMiss(benchmark::State& state) {
  bwarray arr;
  const int n = static_cast<int>(state.range(0));
  for (int i = 0; i < n; ++i) arr.insert(i);
  for (auto _ : state) {
    benchmark::DoNotOptimize(arr.search(n + 1));
  }
}
BENCHMARK(BM_SearchMiss)->Range(8, 1 << 20);

static void BM_Remove(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  for (auto _ : state) {
    state.PauseTiming();
    bwarray arr;
    for (int i = 0; i < n; ++i) arr.insert(i);
    state.ResumeTiming();

    for (int i = 0; i < n; ++i) arr.remove(i);
    benchmark::DoNotOptimize(arr);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_Remove)->Range(8, 1 << 20);

static void BM_RBT_InsertSequential(benchmark::State& state) {
  for (auto _ : state) {
    std::multiset<int> s;
    for (int i = 0; i < state.range(0); ++i) {
      s.insert(i);
    }
    benchmark::DoNotOptimize(s);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_RBT_InsertSequential)->Range(8, 1 << 20);

static void BM_RBT_SearchHit(benchmark::State& state) {
  std::multiset<int> s;
  const int n = static_cast<int>(state.range(0));
  for (int i = 0; i < n; ++i) s.insert(i);
  for (auto _ : state) {
    benchmark::DoNotOptimize(s.find(n / 2));
  }
}
BENCHMARK(BM_RBT_SearchHit)->Range(8, 1 << 20);

static void BM_RBT_SearchMiss(benchmark::State& state) {
  std::multiset<int> s;
  const int n = static_cast<int>(state.range(0));
  for (int i = 0; i < n; ++i) s.insert(i);
  for (auto _ : state) {
    benchmark::DoNotOptimize(s.find(n + 1));
  }
}
BENCHMARK(BM_RBT_SearchMiss)->Range(8, 1 << 20);

static void BM_RBT_Remove(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  for (auto _ : state) {
    state.PauseTiming();
    std::multiset<int> s;
    for (int i = 0; i < n; ++i) s.insert(i);
    state.ResumeTiming();

    for (int i = 0; i < n; ++i) {
      auto it = s.find(i);
      if (it != s.end()) s.erase(it);
    }
    benchmark::DoNotOptimize(s);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_RBT_Remove)->Range(8, 1 << 20);
