#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
  int64_t n, k, x;
  std::cin >> n >> k >> x;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());
  vector<int64_t> gaps;
  for (int i = 1; i < n; ++i) {
    if ((a[i] - a[i - 1]) > x) {
      gaps.push_back(a[i] - a[i - 1]);
    }
  }
  sort(gaps.begin(), gaps.end());
  int64_t res = gaps.size() + 1;

  // 5 3 2
  // 1 1 5 9 9
  // 4 4

  for (auto g : gaps) {
    // Because of gaps condition need_to_remove is at least 1.
    auto need_to_remove = g / x;
    if (g % x == 0) {
      need_to_remove--;
    }

    if (need_to_remove <= k) {
      k -= need_to_remove;
      res--;
    } else {
      break;
    }
  }

  std::cout << res << "\n";

  return 0;
}
