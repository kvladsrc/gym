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
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int mn = a.front();
    int mx = a.back();
    for (size_t idx = 1; idx < n - 1; ++idx) {
      mn = min(mn, a[idx]);
      mx = max(mx, a[idx]);
    }

    auto res = a.back() - mn;
    res = max(res, mx - a.front());

    for (size_t idx = 0; idx < n - 1; ++idx) {
      res = max(res, a[idx] - a[idx + 1]);
    }

    std::cout << res << "\n";
  }

  return 0;
}
