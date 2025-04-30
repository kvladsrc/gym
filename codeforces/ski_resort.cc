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

int64_t in_ranges(int64_t k, int64_t range) {
  int64_t res = 0;
  for (int64_t i = (k - 1); i < range; ++i) {
    res += range - i;
  }
  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int64_t n = 0;
    int64_t k = 0;
    int64_t q = 0;
    std::cin >> n >> k >> q;

    vector<int64_t> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    vector<int64_t> ranges;
    int64_t cur = 0;
    for (int64_t i = 0; i < n; ++i) {
      if (a[i] <= q) {
        cur++;
        continue;
      }

      if (cur > 0) {
        ranges.push_back(cur);
        cur = 0;
      }
    }
    if (cur > 0) {
      ranges.push_back(cur);
    }

    int64_t res = 0;
    for (auto i : ranges) {
      res += in_ranges(k, i);
    }
    std::cout << res << "\n";
  }

  return 0;
}
