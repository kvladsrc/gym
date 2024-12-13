#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct median {
  priority_queue<int> max_heap;
  priority_queue<int, vector<int>, std::greater<int>> min_heap;
  median() : max_heap(), min_heap() {}
  int get_median() {
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
    while ((int(max_heap.size()) - int(min_heap.size())) > 1) {
      min_heap.push(max_heap.top());
      max_heap.pop();
    }
  }
};

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  auto m = median();

  int n;
  std::cin >> n;
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    m.push(buf);
  }
  std::cout << m.get_median() << "\n";

  return 0;
}
