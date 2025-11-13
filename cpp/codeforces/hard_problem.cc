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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  // Idea is to fill the table, where first column in i row shows the
  // minimal cost of operations to get a_i string in original order
  // and the second column shows the cost of operation to able use a_i
  // string in reverse order.

  vector<string> orig(n);
  vector<string> reversed(n);
  for (size_t idx = 0; idx < n; ++idx) {
    std::cin >> orig[idx];
    reversed[idx] = orig[idx];
    std::reverse(reversed[idx].begin(), reversed[idx].end());
  }

  vector<pair<int64_t, int64_t>> dp(n);

  dp.front().first = 0;
  dp.front().second = 0;
  if (orig.front() != reversed.front()) {
    dp.front().second = a.front();
  }

  for (size_t idx = 1; idx < n; ++idx) {
    dp[idx].first = INT64_MAX;
    if (orig[idx] >= orig[idx - 1] && dp[idx - 1].first != INT64_MAX) {
      dp[idx].first = dp[idx - 1].first;
    }
    if (orig[idx] >= reversed[idx - 1] && dp[idx - 1].second != INT64_MAX) {
      dp[idx].first = min(dp[idx].first, dp[idx - 1].second);
    }

    auto cost = a[idx];
    if (orig[idx] == reversed[idx]) {
      cost = 0;
    }

    dp[idx].second = INT64_MAX;
    if (reversed[idx] >= orig[idx - 1] && dp[idx - 1].first != INT64_MAX) {
      dp[idx].second = dp[idx - 1].first + cost;
    }
    if (reversed[idx] >= reversed[idx - 1] && dp[idx - 1].second != INT64_MAX) {
      dp[idx].second = min(dp[idx].second, dp[idx - 1].second + cost);
    }
  }

  auto res = min(dp.back().first, dp.back().second);
  if (res == INT64_MAX) {
    std::cout << -1 << "\n";
  } else {
    std::cout << res << "\n";
  }

  return 0;
}
