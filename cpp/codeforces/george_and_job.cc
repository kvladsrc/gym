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

int64_t solve_rec(vector<int64_t>& pref_sum, int m, int k, int start) {
  if (k == 0) {
    return 0;
  }

  int last = pref_sum.size() - 1;
  int local_last = start - 1 + (m * k);

  if (local_last > last) {
    return INT64_MIN;
  }

  auto case1 = pref_sum[start - 1 + m] - pref_sum[start - 1];
  case1 += solve_rec(pref_sum, m, k - 1, start + m);

  auto case2 = solve_rec(pref_sum, m, k, start + 1);

  return max(case1, case2);
}

int64_t solve_dp(const vector<int64_t>& pref_sum, int m, size_t k) {
  vector<vector<int64_t>> dp(k + 1, vector<int64_t>(pref_sum.size()));
  for (size_t idx = 0; idx < pref_sum.size(); ++idx) {
    dp[0][idx] = 0;
  }

  for (size_t k_idx = 1; k_idx <= k; ++k_idx) {
    for (size_t idx = pref_sum.size() - 1; idx > 0; --idx) {
      auto local_last = idx + (k_idx * m) - 1;
      if (local_last >= pref_sum.size()) {
        dp[k_idx][idx] = INT64_MIN;
        continue;
      }

      auto local = pref_sum[idx + m - 1] - pref_sum[idx - 1];
      if (idx + m < pref_sum.size()) {
        local += dp[k_idx - 1][idx + m];
      }

      if (idx + 1 < pref_sum.size()) {
        dp[k_idx][idx] = max(local, dp[k_idx][idx + 1]);
      } else {
        dp[k_idx][idx] = local;
      }
    }
  }

  return dp[k][1];
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  int k = 0;
  std::cin >> n >> m >> k;

  vector<int64_t> a(n);
  vector<int64_t> pref_sum(n + 1);
  pref_sum.front() = 0;
  for (int idx = 0; idx < n; ++idx) {
    std::cin >> a[idx];
    pref_sum[idx + 1] = pref_sum[idx] + a[idx];
  }

  std::cout << solve_dp(pref_sum, m, k) << "\n";

  return 0;
}
