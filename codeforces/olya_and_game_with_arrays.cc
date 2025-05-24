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
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;
    vector<pair<int64_t, int64_t>> arrays(n);
    for (int i = 0; i < n; ++i) {
      arrays[i] = {INT64_MAX, INT64_MAX};
      int m = 0;
      std::cin >> m;
      for (int j = 0; j < m; ++j) {
        int64_t buf = 0;
        std::cin >> buf;
        if (buf <= arrays[i].first) {
          arrays[i].second = arrays[i].first;
          arrays[i].first = buf;
          continue;
        }

        arrays[i].second = min(arrays[i].second, buf);
      }
    }

    int64_t res = 0;
    int64_t min_second = INT64_MAX;
    int64_t min_first = INT64_MAX;
    for (auto p : arrays) {
      min_first = min(min_first, p.first);
      min_second = min(min_second, p.second);
      res += p.second;
    }
    res -= min_second;
    res += min_first;
    std::cout << res << "\n";
  }

  return 0;
}
