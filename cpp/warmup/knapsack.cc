#include "cpp/warmup/knapsack.hpp"

#include <algorithm>
#include <cstddef>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

namespace warmup {

/*
 * Recurent form:
 *
 * for each item:
 *   case1 = knapsack(rest_items, w - item.w) + item.c
 *   case2 = knapsack(rest_items, w)
 *   return max(case1, case2)
 */

int knapsack(const vector<int>& weights, const vector<int>& costs, int w) {
  // We can pick each item once.
  vector<vector<int>> dp(weights.size() + 1, vector<int>(w + 1));

  // No elements to chose from.
  for (int i = 0; i <= w; ++i) {
    dp[0][i] = 0;
  }

  // No capacity.
  for (std::size_t i = 0; i <= weights.size(); ++i) {
    dp[i][0] = 0;
  }

  for (std::size_t i = 1; i <= weights.size(); ++i) {
    for (int j = 0; j <= w; ++j) {
      if (weights[i - 1] <= j) {
        auto case1 = dp[i - 1][j - weights[i - 1]] + costs[i - 1];
        auto case2 = dp[i - 1][j];
        dp[i][j] = max(case1, case2);
      } else {
        dp[i][j] = dp[i - 1][j];
      }
    }
  }

  return dp.back().back();
}

}  // namespace warmup
