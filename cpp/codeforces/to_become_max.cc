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

bool solve(vector<int> &a, int pos, int k, int val) {
  if (pos >= static_cast<int>(a.size())) {
    return false;
  }

  if (a[pos] >= val) {
    return true;
  }

  if (k < (val - a[pos])) {
    return false;
  }

  return solve(a, pos + 1, k - (val - a[pos]), val - 1);
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    int global = a.back();
    for (size_t idx = 0; idx < n; ++idx) {
      int r = a[idx] + k;
      int l = a[idx];
      int res = -1;

      while (l <= r) {
        int mid = (l + r) / 2;
        if (solve(a, idx, k, mid)) {
          res = mid;
          l = mid + 1;
        } else {
          r = mid - 1;
        }
        global = max(global, res);
      }
    }

    std::cout << global << "\n";
  }

  return 0;
}
