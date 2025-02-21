#include <climits>
#include <iostream>
#include <stack>

using std::stack;

namespace {

class MinStack {
  stack<int> main;
  stack<int> min;

 public:
  MinStack() = default;

  int get_min() {
    if (min.empty()) {
      return INT_MAX;
    }

    return min.top();
  }

  void push(int el) {
    main.push(el);
    if (min.empty() || el <= min.top()) {
      min.push(el);
    }
  }

  bool empty() { return main.empty(); }

  void pop() {
    if (main.empty()) {
      return;
    }

    auto el = main.top();
    main.pop();

    if (el == min.top()) {
      min.pop();
    }
  }
};

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  MinStack s;

  for (int i = 0; i < n; ++i) {
    int buf = 0;
    std::cin >> buf;
    s.push(buf);
  }

  while (!s.empty()) {
    auto min_el = s.get_min();
    s.pop();
    std::cout << min_el << " ";
  }
  std::cout << "\n";

  return 0;
}
