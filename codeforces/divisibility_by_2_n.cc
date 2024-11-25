#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // Product of all a_i must be dividible to 2^n. Lets divide each
    // of a_i by 2 until we can and count number of divisions d. If d
    // >= n we are done, otherwise we need to apply the operation for
    // each even i incrementing d until we done.
    int d = 0;
    for (auto i : a) {
      while (i > 0 && i % 2 == 0) {
        i /= 2;
        d++;
      }
    }

    if (d >= n) {
      std::cout << 0 << "\n";
      continue;
    }

    vector<int> impact;
    for (int i = 1; i <= n; ++i) {
      int buf = i;
      int imp = 0;
      while (buf > 0 && buf % 2 == 0) {
        buf /= 2;
        imp++;
      }

      if (imp > 0) {
        impact.push_back(imp);
      }
    }
    sort(impact.begin(), impact.end());

    int res = 0;
    while (d < n && impact.size() > 0) {
      d += impact.back();
      impact.pop_back();
      res++;
    }

    if (d >= n) {
      std::cout << res << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
