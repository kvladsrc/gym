#include "cpp/warmup/min_stack.hpp"

#include <climits>
#include <stack>

namespace warmup {

MinStack::MinStack() = default;

int MinStack::get_min() {
  if (min.empty()) {
    return INT_MAX;
  }
  return min.top();
}

void MinStack::push(int el) {
  main.push(el);
  if (min.empty() || el <= min.top()) {
    min.push(el);
  }
}

bool MinStack::empty() { return main.empty(); }

void MinStack::pop() {
  if (main.empty()) {
    return;
  }

  auto el = main.top();
  main.pop();

  if (!min.empty() && el == min.top()) {
    min.pop();
  }
}

}  // namespace warmup
