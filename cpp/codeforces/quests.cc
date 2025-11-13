#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
    size_t n = 0;
    size_t k = 0;
    std::cin >> n >> k;

    vector<int> a(n + 1);
    a.front() = 0;
    for (size_t idx = 0; idx < n; ++idx) {
      int buf = 0;
      std::cin >> buf;
      a[idx + 1] = a[idx] + buf;
    }

    vector<int> b(n + 1);
    b.front() = INT_MIN;
    for (size_t idx = 0; idx < n; ++idx) {
      int buf = 0;
      std::cin >> buf;
      b[idx + 1] = max(b[idx], buf);
    }

    int res = INT_MIN;
    for (size_t idx = 0; idx < n; ++idx) {
      if (idx + 1 > k) {
        break;
      }
      auto local = a[idx + 1];
      local += b[idx + 1] * (k - (idx + 1));
      res = max(res, local);
    }

    std::cout << res << "\n";
  }

  return 0;
}
