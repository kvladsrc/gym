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
    size_t n = 0;
    int c = 0;
    std::cin >> n >> c;

    vector<int> a(n);
    vector<int64_t> pref_sum(n + 1);
    pref_sum.front() = 0;
    size_t max_idx = 0;

    for (size_t idx = 0; idx < n; ++idx) {
      std::cin >> a[idx];
      pref_sum[idx + 1] = pref_sum[idx] + a[idx];
      if (a[idx] > a[max_idx]) {
        max_idx = idx;
      }
    }

    if (a.front() + c >= a[max_idx]) {
      max_idx = 0;
    }

    for (size_t idx = 0; idx < n; ++idx) {
      if (idx == max_idx) {
        std::cout << 0 << " ";
        continue;
      }

      auto res = idx;
      if (pref_sum[idx + 1] + c < a[max_idx] && max_idx > idx) {
        res++;
      }
      std::cout << res << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
