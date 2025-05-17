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
    int n = 0;
    std::cin >> n;
    map<int64_t, int64_t> m;
    for (int i = 0; i < n; ++i) {
      int64_t buf = 0;
      std::cin >> buf;
      m[buf]++;
    }

    // a_i + a_j = x
    // a_i * a_j = y
    // a_i = x - a_j
    // (x - a_j) * a_j = y
    // a_j^2 - a_jx + y = 0

    int q = 0;
    std::cin >> q;
    for (int i = 0; i < q; ++i) {
      int64_t x = 0;
      int64_t y = 0;
      std::cin >> x >> y;

      auto D = (x * x) - (4 * y);
      if (D < 0) {
        std::cout << 0 << " ";
        continue;
      }

      int64_t sqrtD = std::llround(std::sqrt(static_cast<double>(D)));
      if (sqrtD * sqrtD != D) {
        std::cout << 0 << " ";
        continue;
      }

      int64_t a1 = ((x + sqrtD) / 2);
      int64_t a2 = ((x - sqrtD) / 2);
      int64_t res = 0;
      if (a1 != a2) {
        res = m[a1] * m[a2];
      } else {
        res = (m[a1] * (m[a1] - 1)) / 2;
      }
      std::cout << res << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
