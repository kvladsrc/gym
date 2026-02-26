#include <benchmark/benchmark.h>

#include <string>

#include "cpp/warmup/boyer_moore.hpp"

static std::string make_text(int n) { return std::string(n, 'a') + "b"; }
static const char kPattern[] = "b";  // NOLINT(runtime/string)

static std::string make_long_pattern(int n) {
  return std::string(n / 4, 'a') + "b";
}

static void BM_BoyerMoore(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::boyer_moore(text, kPattern));
  }
}
BENCHMARK(BM_BoyerMoore)->Range(1 << 8, 1 << 20);

static void BM_BoyerMoore_LongPattern(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  const auto pattern = make_long_pattern(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::boyer_moore(text, pattern));
  }
}
BENCHMARK(BM_BoyerMoore_LongPattern)->Range(1 << 8, 1 << 20);

static void BM_StdStringFind(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(text.find(kPattern));
  }
}
BENCHMARK(BM_StdStringFind)->Range(1 << 8, 1 << 20);

static void BM_StdStringFind_LongPattern(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  const auto pattern = make_long_pattern(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(text.find(pattern));
  }
}
BENCHMARK(BM_StdStringFind_LongPattern)->Range(1 << 8, 1 << 20);
