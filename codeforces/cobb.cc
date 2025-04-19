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

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;

  while ((t--) != 0) {
    int n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int> a(n);
    map<int, int> m;

    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      m[a[i]] = i + 1;
    }

    int64_t res = INT64_MIN;

    for (auto p : m) {
      auto j = (res + (k * p.first)) / p.second;
      j--;
      int64_t zero = 0;
      j = max(j, zero);

      for (int64_t idx = j; idx < (p.second - 1); ++idx) {
        auto local = ((idx + 1) * p.second) - (k * (p.first | a[idx]));
        res = max(res, local);
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
