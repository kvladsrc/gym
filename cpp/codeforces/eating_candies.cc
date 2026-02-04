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

    int l = 0;
    int r = n - 1;

    int64_t l_sum = a.front();
    int64_t r_sum = a.back();
    int res = 0;

    while (l < r) {
      if (l_sum == r_sum) {
        res = (l + 1) + n - r;
      }

      if (l_sum < r_sum) {
        l_sum += a[++l];
        continue;
      }

      r_sum += a[--r];
    }

    std::cout << res << "\n";
  }

  return 0;
}
