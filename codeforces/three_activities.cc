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

struct day_info {
  int day;
  int cnt;
};

bool compare_day(const day_info &a, const day_info &b) { return a.cnt < b.cnt; }

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<day_info> a(n);
    for (int i = 0; i < n; ++i) {
      a[i].day = i;
      std::cin >> a[i].cnt;
    }
    sort(a.begin(), a.end(), compare_day);

    vector<day_info> b(n);
    for (int i = 0; i < n; ++i) {
      b[i].day = i;
      std::cin >> b[i].cnt;
    }
    sort(b.begin(), b.end(), compare_day);

    vector<day_info> c(n);
    for (int i = 0; i < n; ++i) {
      c[i].day = i;
      std::cin >> c[i].cnt;
    }
    sort(c.begin(), c.end(), compare_day);

    // Set of three the "best" days of each activity enought to find
    // best order. No needs to move less prioritized activity far that
    // 3 "best" day. We have 3 * 3 * 3 total combinations, some of
    // them are broken because of conflicts, lets just skip them and
    // fine the maximal value.

    int res = 0;
    for (int f = 1; f <= 3; ++f) {
      auto first = a[a.size() - f];

      for (int s = 1; s <= 3; ++s) {
        auto second = b[b.size() - s];
        if (first.day == second.day) {
          continue;
        }

        for (int t = 1; t <= 3; ++t) {
          auto third = c[c.size() - t];
          if (third.day == first.day || third.day == second.day) {
            continue;
          }

          res = max(res, first.cnt + second.cnt + third.cnt);
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
