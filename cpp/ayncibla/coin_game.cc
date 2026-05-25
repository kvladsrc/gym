#include <cstddef>
#include <iostream>
#include <numeric>
#include <vector>

using std::vector;

struct Result {
  bool calculated;
  int reward;
};

static int optimal_coins(vector<int>& coins, size_t l, size_t r,
                         vector<vector<Result>>& table) {
  if (table[l][r].calculated) {
    return table[l][r].reward;
  }

  if (l == r) {
    table[l][r].calculated = true;
    table[l][r].reward = coins[l];
    return table[l][r].reward;
  }

  int const first_case = optimal_coins(coins, l + 1, r, table);
  int const second_case = optimal_coins(coins, l, r - 1, table);

  int reward = std::accumulate(coins.begin() + l, coins.begin() + r + 1, 0);

  if (first_case <= second_case) {
    table[l][r].calculated = true;
    table[l][r].reward = reward - first_case;
    return table[l][r].reward;
  }

  table[l][r].calculated = true;
  table[l][r].reward = reward - second_case;
  return table[l][r].reward;
}

int main() {
  size_t n = 0;
  std::cin >> n;
  vector<int> coins(n);
  for (size_t i = 0; i < n; ++i) {
    std::cin >> coins[i];
  }
  vector<vector<Result>> table;
  table.resize(n, vector<Result>(n));
  std::cout << optimal_coins(coins, 0, n - 1, table) << "\n";
}
