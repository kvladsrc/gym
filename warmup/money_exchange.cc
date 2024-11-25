#include <algorithm>
#include <iostream>
#include <vector>

using std::min;
using std::vector;

#define MAX 1000000

int exchange(vector<int> &coins, int sum) {
  vector<int> dp(sum + 1, MAX);
  dp[0] = 0;
  for (int i = 1; i <= sum; ++i) {
    int best = MAX;
    for (auto c : coins) {
      if ((i - c) > 0) {
        best = min(best, dp[i - c] + 1);
      }
    }
  }

  return dp.back();
}

int main() {
  vector<int> coins = {1, 3, 4};
  int sum;
  std::cin >> sum;
  std::cout << exchange(coins, sum) << "\n";
}
