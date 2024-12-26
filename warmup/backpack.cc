#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::string;
using std::vector;

// We can pick each item once.
static int backpack(vector<int> &weights, const vector<int> &costs, int w) {
  vector<vector<int>> dp(weights.size() + 1, vector<int>(w + 1));

  for (int i = 0; i <= w; ++i) {
    dp[0][i] = 0;
  }

  for (size_t i = 0; i <= weights.size(); ++i) {
    dp[i][0] = 0;
  }

  // Recurent form:
  //
  // for each item:
  //   case1 = backpack(rest_items, w - item.w) + item.c
  //   case2 = backpack(rest_items, w)
  //   return max(case1, case2)
  for (size_t i = 1; i <= weights.size(); ++i) {
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

  std::cout << backpack(weights, costs, w) << "\n";

  return 0;
}
