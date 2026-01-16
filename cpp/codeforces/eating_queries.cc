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
    int q = 0;
    std::cin >> n >> q;

    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    sort(a.begin(), a.end(), std::greater<int>());

    vector<int> pref_sum(n + 1);
    pref_sum.front() = 0;
    for (int i = 1; i <= n; ++i) {
      pref_sum[i] = pref_sum[i - 1] + a[i - 1];
    }

    for (int i = 0; i < q; ++i) {
      int x = 0;
      std::cin >> x;

      if (x > pref_sum.back()) {
        std::cout << -1 << "\n";
        continue;
      }

      auto it = std::lower_bound(pref_sum.begin(), pref_sum.end(), x);
      std::cout << it - pref_sum.begin() << "\n";
    }
  }

  return 0;
}
