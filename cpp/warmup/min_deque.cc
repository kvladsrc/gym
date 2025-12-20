#include "cpp/warmup/min_deque.hpp"

#include <climits>
#include <cstddef>
#include <deque>
#include <utility>

namespace warmup {

MinDeque::MinDeque(std::size_t size) : max_size(size), cur_pos(0) {}

int MinDeque::get_min() {
  if (deq.empty()) {
    return INT_MAX;
  }
  return deq.front().second;
}

void MinDeque::push_back(int el) {
  // Remove elements from the back that are greater than or equal to the current
  // element. This maintains the deque in strictly increasing order of values.
  while (!deq.empty() && deq.back().second >= el) {
    deq.pop_back();
  }

  deq.emplace_back(cur_pos++, el);

  // Remove elements from the front that are out of the current window.
  // Use subtraction to avoid potential overflow issues with addition.
  while (!deq.empty() && cur_pos - deq.front().first > max_size) {
    deq.pop_front();
  }
}

}  // namespace warmup
