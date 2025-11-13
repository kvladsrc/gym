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

int upper_bound(vector<int> &a, int i) {
  int l = 0;
  int r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    auto const mid = (l + r) / 2;
    if (a[mid] >= i) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
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

    vector<int> l;
    vector<int> r;
    map<int, int> m;
    for (size_t i = 0; i < (n / 2); ++i) {
      int cur = a[i] + a[(a.size() - 1) - i];
      m[cur]++;
      r.push_back(cur + (k - min(a[i], a[(a.size() - 1) - i])));
      l.push_back((cur + 1) - max(a[i], a[(a.size() - 1) - i]));
    }

    sort(r.begin(), r.end());
    sort(l.begin(), l.end());

    int res = n / 2;  // k + 1 sum can be made from every pair

    for (size_t i = 0; i < (n / 2); ++i) {
      int local = n / 2;
      int cur = a[i] + a[(a.size() - 1) - i];
      local -= m[cur];

      // Need two operations.
      auto small = upper_bound(r, cur);
      if (small == -1) {
        small = (n / 2);
      }

      // Need two operations.
      auto big = upper_bound(l, cur + 1);
      if (big == -1) {
        big = 0;
      } else {
        big = (n / 2) - big;
      }

      local += small + big;
      res = min(res, local);
    }

    std::cout << res << "\n";
  }

  return 0;
}
