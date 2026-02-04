#include "cpp/warmup/knapsack_with_duplicates.hpp"

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

namespace warmup {

int knapsack_duplicates(const vector<int>& weights, const vector<int>& costs,
                        int w) {
  vector<int> dp(w + 1, 0);

  // Recurent form:
  //
  // for each item:
  //   case1 = knapsack(items, w - item.w) + item.c
  //   case2 = knapsack(rest_items, w)
  for (std::size_t i = 0; i < weights.size(); ++i) {
    for (int j = 0; j <= w; ++j) {
      if (weights[i] <= j) {
        dp[j] = max(dp[j], dp[j - weights[i]] + costs[i]);
      }
    }
  }

  return dp.back();
}

}  // namespace warmup
