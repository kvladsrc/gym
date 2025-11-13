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

  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  string s;
  std::cin >> s;

  vector<vector<int>> pref_sums;

  for (int fl = 0; fl < 3; ++fl) {
    for (int sl = 0; sl < 3; ++sl) {
      for (int tl = 0; tl < 3; ++tl) {
        if (fl == sl || fl == tl || sl == tl) {
          continue;
        }

        string pref;
        pref.push_back('a' + fl);
        pref.push_back('a' + sl);
        pref.push_back('a' + tl);

        string patt;
        while (patt.size() < s.size()) {
          patt += pref;
        }

        while (patt.size() > s.size()) {
          patt.pop_back();
        }

        vector<int> pref_sum(s.size() + 1);
        pref_sum.front() = 0;

        for (size_t i = 1; i <= s.size(); ++i) {
          pref_sum[i] = pref_sum[i - 1];
          if (s[i - 1] != patt[i - 1]) {
            pref_sum[i]++;
          }
        }

        pref_sums.push_back(std::move(pref_sum));
      }
    }
  }

  for (int i = 0; i < m; ++i) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;

    int best = r - l + 1;
    for (size_t j = 0; j < pref_sums.size(); ++j) {
      best = min(best, pref_sums[j][r] - pref_sums[j][l - 1]);
    }

    std::cout << best << "\n";
  }

  return 0;
}
