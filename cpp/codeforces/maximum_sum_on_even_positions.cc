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
    size_t n = 0;
    std::cin >> n;
    int64_t total = 0;
    vector<int64_t> a(n);
    for (size_t idx = 0; idx < n; ++idx) {
      int64_t buf;
      std::cin >> buf;
      a[idx] = buf;
      if (idx % 2 == 0) {
        total += buf;
      }
    }

    // We need to find subarray with maximal sum if it will be
    // reversed. Operation on subarray is odd number of elements has
    // zero impact on sum. Use modifiyed Kadane's algorithm.

    int64_t global = 0;
    int64_t local = 0;
    for (size_t idx = 1; idx < n; idx += 2) {
      local = max(local + a[idx] - a[idx - 1], a[idx] - a[idx - 1]);
      global = max(global, local);
    }

    local = 0;
    for (size_t idx = 2; idx < n; idx += 2) {
      local = max(local + a[idx - 1] - a[idx], a[idx - 1] - a[idx]);
      global = max(global, local);
    }

    std::cout << total + global << "\n";
  }

  return 0;
}
