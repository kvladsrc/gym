#include "cpp/warmup/lcs.hpp"

#include <algorithm>
#include <cstddef>
#include <vector>

using std::max;
using std::vector;

namespace warmup {

int lcs_dp(const vector<int>& a, const vector<int>& b) {
  vector<vector<int>> dp(a.size() + 1, vector<int>(b.size() + 1));

  for (std::size_t i = 0; i <= a.size(); ++i) {
    dp[i][0] = 0;
  }

  for (std::size_t i = 0; i <= b.size(); ++i) {
    dp[0][i] = 0;
  }

  for (std::size_t a_i = 1; a_i <= a.size(); ++a_i) {
    for (std::size_t b_i = 1; b_i <= b.size(); ++b_i) {
      if (a[a_i - 1] == b[b_i - 1]) {
        dp[a_i][b_i] = dp[a_i - 1][b_i - 1] + 1;
        continue;
      }

      int const case1 = dp[a_i - 1][b_i];
      int const case2 = dp[a_i][b_i - 1];
      dp[a_i][b_i] = max(case1, case2);
    }
  }

  return dp.back().back();
}

int lcs_rec(const vector<int>& a, const vector<int>& b) {
  if (a.empty() || b.empty()) {
    return 0;
  }

  vector<int> const b_case(b.begin() + 1, b.end());
  vector<int> const a_case(a.begin() + 1, a.end());

  if (a[0] == b[0]) {
    return lcs_rec(a_case, b_case) + 1;
  }

  return max(lcs_rec(a, b_case), lcs_rec(a_case, b));
}

}  // namespace warmup
