#include <benchmark/benchmark.h>

#include <queue>

#include "cpp/warmup/binary_heap.hpp"

static void BM_HeapPush(benchmark::State& state) {
  for (auto _ : state) {
    warmup::BinaryHeap h;
    for (int i = 0; i < state.range(0); ++i) {
      h.push(i);
    }
    benchmark::DoNotOptimize(h);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_HeapPush)->Range(8, 1 << 20);

static void BM_HeapPushPop(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    warmup::BinaryHeap h;
    for (int i = 0; i < state.range(0); ++i) h.push(i);
    state.ResumeTiming();
    while (!h.empty()) {
      benchmark::DoNotOptimize(h.top());
      h.pop();
    }
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_HeapPushPop)->Range(8, 1 << 20);

static void BM_StdPriorityQueuePush(benchmark::State& state) {
  for (auto _ : state) {
    std::priority_queue<int> pq;
    for (int i = 0; i < state.range(0); ++i) {
      pq.push(i);
    }
    benchmark::DoNotOptimize(pq);
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_StdPriorityQueuePush)->Range(8, 1 << 20);

static void BM_StdPriorityQueuePushPop(benchmark::State& state) {
  for (auto _ : state) {
    state.PauseTiming();
    std::priority_queue<int> pq;
    for (int i = 0; i < state.range(0); ++i) pq.push(i);
    state.ResumeTiming();
    while (!pq.empty()) {
      int top = pq.top();
      benchmark::DoNotOptimize(top);
      pq.pop();
    }
  }
  state.SetItemsProcessed(state.iterations() * state.range(0));
}
BENCHMARK(BM_StdPriorityQueuePushPop)->Range(8, 1 << 20);
