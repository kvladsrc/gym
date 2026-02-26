#include <benchmark/benchmark.h>

#include <string>

#include "cpp/warmup/knuth_morris_pratt.hpp"

// Text: 'a' repeated N times, pattern at the very end (worst case for naive).
static std::string make_text(int n) { return std::string(n, 'a') + "b"; }
static const char kPattern[] = "b";  // NOLINT(runtime/string)

// Longer pattern: forces more prefix-function computation.
static std::string make_long_pattern(int n) {
  // Pattern of n/4 'a's followed by 'b' — appears once at end of text.
  return std::string(n / 4, 'a') + "b";
}

static void BM_KMP(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::kmp(text, kPattern));
  }
}
BENCHMARK(BM_KMP)->Range(1 << 8, 1 << 20);

static void BM_KMP_LongPattern(benchmark::State& state) {
  const int n = static_cast<int>(state.range(0));
  const auto text = make_text(n);
  const auto pattern = make_long_pattern(n);
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::kmp(text, pattern));
  }
}
BENCHMARK(BM_KMP_LongPattern)->Range(1 << 8, 1 << 20);

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
