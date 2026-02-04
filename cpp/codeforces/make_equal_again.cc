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
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    int l = 0;
    int r = n - 1;
    while (l < n && a[l] == a.front()) {
      l++;
    }
    while (r > l && a[r] == a.front()) {
      r--;
    }
    int case1 = (r - l) + 1;

    l = 0;
    r = n - 1;
    while (r >= 0 && a[r] == a.back()) {
      r--;
    }
    while (l < r && a[l] == a.back()) {
      l++;
    }
    int case2 = (r - l) + 1;

    std::cout << min(case1, case2) << "\n";
  }

  return 0;
}
