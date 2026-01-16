#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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
    int n = 0;
    int64_t q = 0;
    std::cin >> n;
    std::cin >> q;

    vector<int64_t> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int64_t l = 0;
    int64_t r = std::sqrt(static_cast<double>(q / n));
    int64_t res = 0;

    while (l <= r) {
      auto m = (l + r) / 2;

      int64_t local =
          std::accumulate(a.begin(), a.end(), 0LL, [m](int64_t acc, int64_t h) {
            h += (m * 2);
            return acc + (h * h);
          });

      if (local >= q) {
        res = m;
        r = m - 1;
      } else {
        l = m + 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
