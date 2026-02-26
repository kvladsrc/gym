#include <benchmark/benchmark.h>

#include <numeric>

#include "cpp/warmup/gcd.hpp"

// Use Fibonacci-adjacent pairs: worst case for Euclidean algorithm.
// F(n) and F(n+1) maximise the number of steps.
static constexpr int kA = 832040;   // F(30)
static constexpr int kB = 1346269;  // F(31)

static void BM_GCD(benchmark::State& state) {
  for (auto _ : state) {
    benchmark::DoNotOptimize(warmup::gcd(kA, kB));
  }
}
BENCHMARK(BM_GCD);

static void BM_StdGCD(benchmark::State& state) {
  for (auto _ : state) {
    benchmark::DoNotOptimize(std::gcd(kA, kB));
  }
}
BENCHMARK(BM_StdGCD);
