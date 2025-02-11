#include <algorithm>
#include <cmath>
#include <cstddef>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

static int knapsack_duplicates(const vector<int> &weights,
                               const vector<int> &costs, int w) {
  vector<int> dp(w + 1, 0);

  // Recurent form:
  //
  // for each item:
  //   case1 = knapsack(items, w - item.w) + item.c
  //   case2 = knapsack(rest_items, w)
  for (size_t i = 0; i < weights.size(); ++i) {
    for (int j = 0; j <= w; ++j) {
      if (weights[i] <= j) {
        dp[j] = max(dp[j], dp[j - weights[i]] + costs[i]);
      }
    }
  }

  return dp.back();
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  int w = 0;
  std::cin >> n >> w;

  vector<int> weights(n);
  for (auto &i : weights) {
    std::cin >> i;
  }

  vector<int> costs(n);
  for (auto &i : costs) {
    std::cin >> i;
  }

  std::cout << knapsack_duplicates(weights, costs, w) << "\n";

  return 0;
}
