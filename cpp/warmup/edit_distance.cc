#include "cpp/warmup/edit_distance.hpp"

#include <algorithm>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::min;
using std::string;
using std::vector;

namespace warmup {

/*
 * Classical dynamic programming algorithm for calculating a minimal
 * number of changes to get string b from a. Single change can be one
 * of the operations:
 *
 * - Remove char;
 * - Replace char;
 * - Incert a char.
 */
int edit_distance(string const& a, string const& b) {
  vector<vector<int>> dp(a.size() + 1, vector<int>(b.size() + 1));
  for (std::size_t i = 0; i <= a.size(); ++i) {
    dp[i][0] = static_cast<int>(i);  // Remove all.
  }
  for (std::size_t i = 0; i <= b.size(); ++i) {
    dp[0][i] = static_cast<int>(i);  // Add all to empty.
  }

  for (std::size_t a_idx = 1; a_idx <= a.size(); ++a_idx) {
    for (std::size_t b_idx = 1; b_idx <= b.size(); ++b_idx) {
      /*
       * Recurrent form:
       * ed(a, b) -- edit distance from a to b:
       *   if (a.empty()) return b.size();
       *   if (b.empty()) return a.size();
       *   if (a.front() == b.front) return ed(a.begin() + 1, b.begin() + 1);
       *
       *   case_delete = 1 + ed(a.begin() + 1, b);
       *   case_replace = 1 + ed(a.begin() + 1, b.begin() + 1);
       *   case_incert = 1 + ed(a, b.begin() + 1);
       *
       *   return min(case_delete, case_replace, case_incert);
       */
      if (a[a_idx - 1] == b[b_idx - 1]) {
        dp[a_idx][b_idx] = dp[a_idx - 1][b_idx - 1];
        continue;
      }
      auto case_delete = 1 + dp[a_idx - 1][b_idx];
      auto case_replace = 1 + dp[a_idx - 1][b_idx - 1];
      auto case_incert = 1 + dp[a_idx][b_idx - 1];
      dp[a_idx][b_idx] = min({case_delete, case_replace, case_incert});
    }
  }

  return dp.back().back();
}

}  // namespace warmup
