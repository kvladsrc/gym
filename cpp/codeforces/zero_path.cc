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

template <typename Op>
int op_path(const vector<vector<int>>& table, Op op) {
  int n = table.size();
  int m = table.front().size();
  vector<vector<int>> dp(n, vector<int>(m));

  dp[n - 1][m - 1] = table[n - 1][m - 1];
  for (int i = 1; i < m; ++i) {
    dp[n - 1][m - 1 - i] = dp[n - 1][m - i];
    dp[n - 1][m - 1 - i] += table[n - 1][m - 1 - i];
  }

  for (int i = 1; i < n; ++i) {
    dp[n - 1 - i][m - 1] = dp[n - i][m - 1];
    dp[n - 1 - i][m - 1] += table[n - 1 - i][m - 1];
  }

  for (int j = 1; j < m; ++j) {
    for (int i = 1; i < n; ++i) {
      dp[n - 1 - i][m - 1 - j] = op(dp[n - i][m - 1 - j], dp[n - 1 - i][m - j]);
      dp[n - 1 - i][m - 1 - j] += table[n - 1 - i][m - 1 - j];
    }
  }

  return dp.front().front();
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  /*
   * Let S(x) is the sum of squares on path x. If exists p1, S(p1) > 0
   * and p2, S(p2) < 0, and n + m is even -> exists p3, S(p3) =
   * 0. Because every path can be transormed to another by simple one
   * sqare replacement and every such transformation change sum by 2.
   */

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    int m = 0;
    std::cin >> n >> m;

    vector<vector<int>> table(n, vector<int>(m));

    for (int i = 0; i < n; ++i) {
      for (int j = 0; j < m; ++j) {
        std::cin >> table[i][j];
      }
    }

    if ((n + m) % 2 == 0) {
      std::cout << "NO" << "\n";
      continue;
    }

    auto min_path = op_path(table, [](int a, int b) { return std::min(a, b); });
    auto max_path = op_path(table, [](int a, int b) { return std::max(a, b); });

    if (max_path >= 0 && min_path <= 0) {
      std::cout << "YES" << "\n";
    } else {
      std::cout << "NO" << "\n";
    }
  }

  return 0;
}
