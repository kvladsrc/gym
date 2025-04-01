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

int64_t solve_rec(vector<int64_t> &a, size_t l, size_t r,
                  vector<vector<int64_t>> &dp) {
  if (l >= r) {
    return 0;
  }

  if (dp[l][r] != -1) {
    return dp[l][r];
  }

  auto case1 = solve_rec(a, l + 1, r, dp) + (a[r] - a[l]);
  auto case2 = solve_rec(a, l, r - 1, dp) + (a[r] - a[l]);

  dp[l][r] = min(case1, case2);
  return dp[l][r];
}

int64_t solve_dp(vector<int64_t> &a) {
  vector<vector<int64_t>> dp(a.size(), vector<int64_t>(a.size()));

  for (size_t i = 0; i < a.size(); ++i) {
    dp[i][i] = 0;
  }

  for (size_t li = 0; li < a.size(); ++li) {
    auto l = a.size() - 1 - li;
    for (size_t r = l + 1; r < a.size(); ++r) {
      auto case1 = dp[l + 1][r];
      auto case2 = dp[l][r - 1];
      dp[l][r] = a[r] - a[l] + min(case1, case2);
    }
  }

  return dp[0][a.size() - 1];
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());
  std::cout << solve_dp(a) << "\n";

  return 0;
}
