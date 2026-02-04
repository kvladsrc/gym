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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    uint64_t n;
    std::cin >> n;

    uint64_t l = 0, r = (sqrt(n) + 1) * 2;
    uint64_t res = -1;
    while (l <= r) {
      uint64_t mid = (l + r) / 2;
      auto can_make = (mid * (mid - 1)) / 2;
      if (can_make <= n) {
        res = mid;
        l = mid + 1;
      } else {
        r = mid - 1;
      }
    }

    res += n - (res * (res - 1) / 2);
    std::cout << res << "\n";
  }

  return 0;
}
