#include <algorithm>
#include <iostream>
#include <vector>

using std::vector;

int min_larger_sum(vector<int> &coins, int sum) {
  int res = 0, cur = 0;
  std::sort(coins.begin(), coins.end());

  int i = coins.size() - 1;
  while (cur <= sum / 2) {
    cur += coins[i--];
    res++;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int n, sum = 0;
  std::cin >> n;
  vector<int> coins(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> coins[i];
    sum += coins[i];
  }
  std::cout << min_larger_sum(coins, sum) << "\n";

  return 0;
}
