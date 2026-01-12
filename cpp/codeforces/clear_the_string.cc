#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
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

int solve(string& s, int start, int end, vector<vector<int>>& dp) {
  if (start == end) {
    return 1;
  }

  if (start > end) {
    return 0;
  }

  if (dp[start][end] != -1) {
    return dp[start][end];
  }

  dp[start][end] = solve(s, start + 1, end, dp) + 1;
  for (int i = start + 1; i <= end; ++i) {
    if (s[start] == s[i]) {
      auto case1 = solve(s, start + 1, i, dp);
      case1 += solve(s, i + 1, end, dp);
      dp[start][end] = min(dp[start][end], case1);
    }
  }

  return dp[start][end];
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  string s;
  std::cin >> s;

  vector<vector<int>> dp(n, vector<int>(n, -1));

  std::cout << solve(s, 0, n - 1, dp) << "\n";

  return 0;
}
