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

namespace {

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

class median {
  priority_queue<int> max_heap;
  priority_queue<int, vector<int>, std::greater<>> min_heap;

 public:
  median() = default;

  optional<int> get_median() const {
    // EDGE_CASE: Cannot get median of empty list.
    if (!max_heap.empty()) {
      return max_heap.top();
    }
    return std::nullopt;
  }

  void push(int el) {
    if (max_heap.empty() || el <= max_heap.top()) {
      max_heap.push(el);
    } else {
      min_heap.push(el);
    }
    fixup();
  }

 private:
  void fixup() {
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
};

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  auto m = median();

  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    m.push(buf);
  }

  auto r = m.get_median();
  if (r.has_value()) {
    std::cout << r.value() << "\n";
  } else {
    std::cout << 0 << "\n";
  }

  return 0;
}
