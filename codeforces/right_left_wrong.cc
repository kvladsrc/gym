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
using std::gcd;
using std::map;
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

    vector<int64_t> pref_sums(n + 1);
    pref_sums[0] = 0;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      pref_sums[i] = pref_sums[i - 1] + buf;
    }

    vector<char> symbols(n);
    for (auto &c : symbols) {
      std::cin >> c;
    }

    int l = 0, r = n - 1;
    int64_t res = 0;
    bool want_l = true;
    bool want_r = true;
    while (true) {
      while (want_l && l < n) {
        if (symbols[l] == 'L') {
          want_l = false;
          break;
        } else {
          l++;
        }
      }

      while (want_r && r >= 0) {
        if (symbols[r] == 'R') {
          want_r = false;
          break;
        } else {
          r--;
        }
      }

      if (l > r) {
        break;
      }

      // 0 1 2 3 4 5
      // . . x x x .
      // Whole sum = pref_sums[n] - pref_sums[0], r starts at n - 1.
      res += pref_sums[r-- + 1] - pref_sums[l++];
      want_l = true;
      want_r = true;
    }

    std::cout << res << "\n";
  }

  return 0;
}
