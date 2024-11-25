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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int64_t n, k;
  std::cin >> n >> k;
  vector<int64_t> a(n), b(n);

  // Total gramms and gramms for one cookie to calculate max value for
  // binary search.
  int64_t one_cookie = 0;
  int64_t total = k;

  for (auto &i : a) {
    std::cin >> i;
    one_cookie += i;
  }

  for (auto &i : b) {
    std::cin >> i;
    total += i;
  }

  // We cannot have more than r cookies.
  int64_t r = (total / one_cookie) + 1;
  int64_t l = 0;
  int64_t res = 0;

  while (l <= r) {
    bool can_cook = true;
    int mid = (l + r) / 2;
    auto k_cpy = k;

    for (int i = 0; i < n; ++i) {
      if (b[i] >= (a[i] * mid)) {
        continue;
      }

      if (k_cpy >= (a[i] * mid) - b[i]) {
        k_cpy -= (a[i] * mid) - b[i];
        continue;
      }

      can_cook = false;
      break;
    }

    if (can_cook) {
      l = mid + 1;
      res = mid;
    } else {
      r = mid - 1;
    }
  }

  std::cout << res << "\n";

  return 0;
}
