#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  vector<int64_t> b(n);
  for (auto &i : b) {
    std::cin >> i;
  }

  // Problem is to optimize naive O(n^3) to naive O(n^2).

  int64_t pref_sum = 0;
  for (size_t idx = 0; idx < n; ++idx) {
    pref_sum += a[idx] * b[idx];
  }

  int64_t global = pref_sum;
  for (size_t idx = 1; idx < n; ++idx) {
    auto local_even = pref_sum;
    auto local_odd = pref_sum;

    for (size_t lr = 1; lr <= idx && (idx + lr) <= n; ++lr) {
      if ((idx + lr) < n) {
        local_odd -= a[idx - lr] * b[idx - lr];
        local_odd -= a[idx + lr] * b[idx + lr];
        local_odd += a[idx - lr] * b[idx + lr];
        local_odd += a[idx + lr] * b[idx - lr];
      }

      local_even -= a[idx - lr] * b[idx - lr];
      local_even -= a[idx + (lr - 1)] * b[idx + (lr - 1)];
      local_even += a[idx - lr] * b[idx + (lr - 1)];
      local_even += a[idx + (lr - 1)] * b[idx - lr];

      global = max(global, local_odd);
      global = max(global, local_even);
    }
  }

  std::cout << global << "\n";

  return 0;
}
