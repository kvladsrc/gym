#include <algorithm>
#include <climits>
#include <cmath>
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
    vector<int64_t> a(n);
    vector<int64_t> b(n);
    for (size_t idx = 0; idx < n; ++idx) {
      std::cin >> a[idx] >> b[idx];
    }

    int64_t total_post = 0;
    for (size_t idx = 0; idx < n; ++idx) {
      auto prev = idx;
      if (prev != 0) {
        prev--;
      } else {
        prev = n - 1;
      }

      if ((a[idx] - b[prev]) > 0) {
        total_post += a[idx] - b[prev];
      }
    }

    int64_t global = INT64_MAX;
    for (size_t idx = 0; idx < n; ++idx) {
      auto prev = idx;
      if (prev != 0) {
        prev--;
      } else {
        prev = n - 1;
      }

      int64_t local = total_post;
      if ((a[idx] - b[prev]) > 0) {
        local -= a[idx] - b[prev];
      }

      local += a[idx];

      global = min(global, local);
    }

    std::cout << global << "\n";
  }

  return 0;
}
