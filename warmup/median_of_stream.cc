#include "cpp/warmup/median_of_stream.hpp"

#include <functional>
#include <iostream>
#include <optional>
#include <queue>
#include <string>
#include <vector>

using std::optional;
using std::priority_queue;
using std::string;
using std::vector;

namespace warmup {

/*
 * Median class implements a data structure with the following
 * operations with logarithmic runtime:
 *
 * - get_median(): Returns a median value if list not empty,
 *                 std::nullopt otherwise. O(1) runtime.
 *
 * - push(int): Add element to the list. O(log N), N -- number of
 *              already added elements.
 */

median::median() = default;

optional<int> median::get_median() const {
  // EDGE_CASE: Cannot get median of empty list.
  if (!max_heap.empty()) {
    return max_heap.top();
  }
  return std::nullopt;
}

void median::push(int el) {
  if (max_heap.empty() || el <= max_heap.top()) {
    max_heap.push(el);
  } else {
    min_heap.push(el);
  }
  fixup();
}

void median::fixup() {
  while (max_heap.size() < min_heap.size()) {
    max_heap.push(min_heap.top());
    min_heap.pop();
  }

  // max_heap.size() >= min_heap.size() here.
  while ((static_cast<int>(max_heap.size()) -
          static_cast<int>(min_heap.size())) > 1) {
    min_heap.push(max_heap.top());
    max_heap.pop();
  }
}

}  // namespace warmup
