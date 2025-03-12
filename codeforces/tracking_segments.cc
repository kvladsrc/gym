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

int main(int /*argc*/, char * /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    vector<pair<int, int>> segments(m);
    for (auto &s : segments) {
      std::cin >> s.first >> s.second;
    }

    int q = 0;
    std::cin >> q;
    vector<int> operations(q);
    for (auto &o : operations) {
      std::cin >> o;
    }

    int l = 0;
    int r = q - 1;
    int res = -1;
    while (l <= r) {
      auto mid = (l + r) / 2;

      vector<bool> a(n, false);
      for (int o = 0; o <= mid; ++o) {
        a[operations[o] - 1] = true;
      }

      vector<int> zeroes(n + 1);
      zeroes.front() = 0;
      for (int i = 0; i < n; ++i) {
        zeroes[i + 1] = zeroes[i];
        if (!a[i]) {
          zeroes[i + 1]++;
        }
      }

      bool found = false;
      for (auto s : segments) {
        auto z = zeroes[s.second] - zeroes[s.first - 1];
        auto len = s.second - s.first + 1;
        if (z * 2 >= len) {
          continue;
        }

        found = true;
        break;
      }

      if (found) {
        res = mid + 1;
        r = mid - 1;
      } else {
        l = mid + 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
