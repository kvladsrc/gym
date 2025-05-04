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

int solve(vector<int> &a, int back) {
  vector<int> buf(a.size(), 0);
  int res = back;
  for (size_t idx = a.size() - 1; idx >= 2; --idx) {
    auto b = a[idx] + buf[idx];
    if (b > res) {
      auto avail = min(a[idx], b - res);
      buf[idx - 1] += avail / 3;
      buf[idx - 2] += (avail / 3) * 2;
      b -= avail - (avail % 3);
    }
    res = min(res, b);
  }
  res = min(res, a[0] + buf[0]);
  res = min(res, a[1] + buf[1]);
  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    int l = 1;
    int r = a.back();
    int res = INT_MIN;

    while (l <= r) {
      int m = (l + r) / 2;
      auto q = solve(a, m);
      if (q < m) {
        r = m - 1;
      } else {
        l = m + 1;
      }
      res = max(res, q);
    }
    std::cout << res << "\n";
  }

  return 0;
}
