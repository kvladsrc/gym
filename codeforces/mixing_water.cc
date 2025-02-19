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

long double average_temp(long double hot_cups, long double h, long double c) {
  long double res = (h + c) * hot_cups;
  res -= c;
  res /= (hot_cups * 1.0 + (hot_cups - 1));
  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int h = 0;
    int c = 0;
    int t = 0;
    std::cin >> h >> c >> t;

    if (t == (h + c) / 2) {
      std::cout << 2 << "\n";
      continue;
    }

    if (t == h) {
      std::cout << 1 << "\n";
      continue;
    }

    // Idea is to use binary search. Let i be a number of hot cups. We
    // do not interested in cold cups because of we always get a (H +
    // C) / 2 temp after each cold cup.

    int64_t l = 1;
    int64_t r = 10000000;
    int64_t res = -1;
    auto tt = static_cast<long double>(t);

    while (l <= r) {
      auto mid = (l + r) / 2;
      auto av = average_temp(mid, h, c);

      if (av <= tt) {
        r = mid - 1;
      } else {
        res = mid;
        l = mid + 1;
      }
    }

    if (res == -1 || res == 10000000) {
      // To close to average;
      std::cout << 2 << "\n";
    } else {
      auto next = average_temp(res + 1, h, c);
      auto cur = average_temp(res, h, c);

      if (abs(cur - tt) <= abs(next - tt)) {
        std::cout << res + res - 1 << "\n";
      } else {
        std::cout << res + 1 + res << "\n";
      }
    }
  }

  return 0;
}
