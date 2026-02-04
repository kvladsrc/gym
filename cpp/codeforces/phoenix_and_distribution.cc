#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int c_to_i(char a) { return a - 'a'; }
char i_to_c(int a) { return 'a' + a; }

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int k = 0;
    int n = 0;
    std::cin >> n >> k;

    priority_queue<int, vector<int>, std::greater<>> min_heap;
    for (int i = 0; i < n; ++i) {
      char buf = 'a';
      std::cin >> buf;
      min_heap.push(c_to_i(buf));
    }

    vector<string> res(k);
    for (int i = 0; i < k; ++i) {
      res[i] = i_to_c(min_heap.top());
      min_heap.pop();
    }

    if (res.front() != res.back()) {
      std::cout << res.back() << "\n";
      continue;
    }

    string case1 = res.back();

    size_t idx = 0;
    while (!min_heap.empty()) {
      auto cur = min_heap.top();
      min_heap.pop();
      res[idx].push_back(i_to_c(cur));
      idx = (idx + 1) % k;
      case1.push_back(i_to_c(cur));
    }

    string case2 = res.front();
    for (const auto& s : res) {
      case2 = max(s, case2);
    }

    std::cout << min(case1, case2) << "\n";
  }

  return 0;
}
