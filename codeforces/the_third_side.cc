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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    std::priority_queue<int, std::vector<int>, std::greater<>> min_heap;
    for (int i = 0; i < n; ++i) {
      int buf = 0;
      std::cin >> buf;
      min_heap.push(buf);
    }

    while (min_heap.size() != 1) {
      int a = min_heap.top();
      min_heap.pop();
      int b = min_heap.top();
      min_heap.pop();
      min_heap.push(a + b - 1);
    }

    std::cout << min_heap.top() << "\n";
  }

  return 0;
}
