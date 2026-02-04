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
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end());

    vector<int64_t> pref_sum(n + 1);
    pref_sum.front() = 0;
    for (size_t i = 1; i <= n; ++i) {
      pref_sum[i] = pref_sum[i - 1] + a[i - 1];
    }

    int64_t mx = INT64_MIN;
    for (int left = 0; left <= k; ++left) {
      int64_t local = pref_sum[n - (k - left)] - pref_sum[left * 2];
      mx = max(mx, local);
    }

    std::cout << mx << "\n";
  }

  return 0;
}
