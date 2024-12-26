#include <functional>
#include <iostream>
#include <queue>
#include <string>
#include <vector>

using std::priority_queue;
using std::string;
using std::vector;

struct median {
  priority_queue<int> max_heap;
  priority_queue<int, vector<int>, std::greater<>> min_heap;
  median() = default;
  int get_median() const {
    if (!max_heap.empty()) {
      return max_heap.top();
    }
    return 0;
  }
  void push(int el) {
    if (max_heap.empty() || el <= max_heap.top()) {
      max_heap.push(el);
    } else {
      min_heap.push(el);
    }
    fixup();
  }
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  auto m = median();

  int n = 0;
  std::cin >> n;
  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    m.push(buf);
  }
  std::cout << m.get_median() << "\n";

  return 0;
}
