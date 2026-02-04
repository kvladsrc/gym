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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    int64_t k = 0;
    std::cin >> k;

    vector<int64_t> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    /*
     * 1. Sort a in non-decreasing order.
     *
     * 2. For every i, 1 <= i <= n, find the minimal number of
     * operations: set a_1 value to X, then all elements a_j = x,
     * i < j <= n, SUM(a) <= k;
     */

    sort(a.begin(), a.end());

    vector<int64_t> pref_sum(n + 1);
    pref_sum.front() = 0;
    for (size_t idx = 0; idx < n; ++idx) {
      pref_sum[idx + 1] = a[idx] + pref_sum[idx];
    }

    auto extra = pref_sum.back() - k;
    if (extra <= 0) {
      std::cout << 0 << "\n";
      continue;
    }

    int64_t global = extra;
    for (size_t idx = 1; idx < n; ++idx) {
      int64_t d = k - (pref_sum[idx] - pref_sum[1]);
      int64_t local_ops = n - idx;
      int64_t maximal_min = 0;

      if (d < 0) {
        d = abs(d);
        maximal_min = d / (n - idx + 1);
        if (d % (n - idx + 1) != 0) {
          maximal_min++;
        }
        maximal_min *= -1;
      } else {
        maximal_min = d / (n - idx + 1);
      }

      if (maximal_min < a.front()) {
        local_ops += a.front() - maximal_min;
      }
      global = min(local_ops, global);
    }

    std::cout << global << "\n";
  }

  return 0;
}
