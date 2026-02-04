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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t n, k;
  std::cin >> n >> k;

  // Simple implementation with binary search. Guess, O(1) solution
  // exists also.

  int64_t l = 0, r = n;
  int64_t res = -1;
  while (l <= r) {
    int64_t mid = (l + r) / 2;
    auto local = mid;
    auto den = k;
    while (mid / den) {
      local += mid / den;
      den *= k;
    }

    if (local >= n) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }

  std::cout << res << "\n";

  return 0;
}
