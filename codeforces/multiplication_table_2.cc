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

int64_t y_at_1_pos(int64_t y, int64_t n, int64_t m) {
  int64_t res = 0;

  for (int64_t x = 1; x <= m; ++x) {
    auto d = min(n, y / x);

    if (d == 0) {
      break;
    }

    res += d;
  }

  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t n = 0;
  int64_t m = 0;
  int64_t k = 0;

  std::cin >> n >> m >> k;

  int64_t l = 1;
  int64_t r = m * n;
  int64_t res = 1;
  int64_t pos = 0;

  while (l <= r) {
    auto mid = (l + r) / 2;
    pos = y_at_1_pos(mid, n, m);

    if (pos < k) {
      res = mid;
      l = mid + 1;
    } else {
      r = mid - 1;
    }
  }

  pos = y_at_1_pos(res, n, m);

  vector<int64_t> largest_per_col_pre(m);
  int64_t last = 0;
  for (int64_t x = 1; x <= m; ++x) {
    auto y = min(n, res / x);
    largest_per_col_pre[x - 1] = y;
    last = max(last, x * y);
  }

  res++;
  vector<int64_t> next;
  for (int64_t x = 1; x <= m; ++x) {
    auto y = min(n, res / x);
    if (largest_per_col_pre[x - 1] != y) {
      next.push_back(x * y);
    }
  }

  if (pos == k) {
    std::cout << last << "\n";
  } else {
    sort(next.begin(), next.end());
    std::cout << next[(k - pos) - 1] << "\n";
  }

  return 0;
}
