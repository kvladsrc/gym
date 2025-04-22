#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
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

int solve_up(vector<int64_t>& a) {
  int res = 0;
  int64_t cur = 0;
  priority_queue<int64_t> heap;

  for (auto i : a) {
    cur += i;

    if (i < 0) {
      heap.push(abs(i));
    }

    while (cur < 0) {
      res++;
      auto l = heap.top();
      heap.pop();
      cur += (2 * l);
    }
  }

  return res;
}

int solve_down(vector<int64_t>& a) {
  int res = 0;
  int64_t cur = 0;
  priority_queue<int64_t> heap;

  for (size_t idx = 0; idx < a.size(); ++idx) {
    auto i = a[idx];
    cur += i;

    if (i > 0) {
      heap.push(i);
    }

    // EDGE_CASE: Very nasty edge case. Spend a lot of time finding
    // it.
    while (cur > 0 && idx < a.size() - 1) {
      res++;
      auto l = heap.top();
      heap.pop();
      cur -= (2 * l);
    }
  }

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    vector<int64_t> left;
    for (auto i = 0; i < m; ++i) {
      int64_t buf = 0;
      std::cin >> buf;
      left.push_back(buf);
      n--;
    }

    vector<int64_t> right;
    for (auto i = 0; i < n; ++i) {
      int64_t buf = 0;
      std::cin >> buf;
      right.push_back(buf);
    }

    // EDGE_CASE: we expect a[m] be negative every time. Only case it
    // can be not true if m == 1.
    auto res = solve_up(right);
    if (m != 1) {
      std::reverse(left.begin(), left.end());
      res += solve_down(left);
    }

    std::cout << res << "\n";
  }

  return 0;
}
