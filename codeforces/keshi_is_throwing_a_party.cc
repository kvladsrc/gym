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

bool solve(vector<pair<int, int>>& f, int m) {
  int cur = 0;
  for (auto p : f) {
    if (p.first < cur) {
      continue;
    }
    if (p.second < (m - cur - 1)) {
      continue;
    }
    cur++;
    if (cur == m) {
      return true;
    }
  }
  return false;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    vector<pair<int, int>> friends(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> friends[i].second >> friends[i].first;
    }

    int l = 1;
    int r = n;
    int res = -1;

    while (l <= r) {
      auto m = (l + r) / 2;
      auto s = solve(friends, m);
      if (s) {
        res = m;
        l = m + 1;
      } else {
        r = m - 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
