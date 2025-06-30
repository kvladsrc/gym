#include "cpp/warmup/binary_heap.hpp"

#include <cstddef>
#include <utility>

namespace warmup {

/*
 * The binary heap implemention uses a vector for storing the tree.
 * From the heap's point of view indexes starts from 1, otherwise
 * second node should have 0 * 2 = 0 index.
 */

void BinaryHeap::push(int el) {
  heap.push_back(el);
  std::size_t idx = heap.size();

  while (idx > 1 && heap[idx - 1] < heap[parent(idx) - 1]) {
    std::swap(heap[idx - 1], heap[parent(idx) - 1]);
    idx = parent(idx);
  }
}

void BinaryHeap::pop() {
  if (heap.empty()) {
    return;
  }

  std::swap(heap.front(), heap.back());
  heap.pop_back();

  std::size_t idx = 1;
  while (true) {
    auto l = l_child(idx);
    auto r = r_child(idx);

    // Cases: l_idx and r_idx available, only l_idx is available, both
    // unavailable.
    std::size_t min_child = 0;
    if (l <= heap.size() && r <= heap.size()) {
      min_child = (heap[l - 1] < heap[r - 1] ? l : r);
    } else if (l <= heap.size()) {
      min_child = l;
    } else {
      break;
    }

    if (heap[idx - 1] > heap[min_child - 1]) {
      std::swap(heap[idx - 1], heap[min_child - 1]);
      idx = min_child;
    } else {
      break;
    }
  }
}

}  // namespace warmup
