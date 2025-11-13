#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <iterator>
#include <limits>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> first(n);
  vector<int> second(n);
  for (auto &i : first) {
    std::cin >> i;
  }
  for (auto &i : second) {
    std::cin >> i;
  }

  if (n == 1) {
    std::cout << max(first[0], second[0]) << "\n";
    return 0;
  }

  std::reverse(first.begin(), first.end());
  std::reverse(second.begin(), second.end());

  vector<vector<int64_t>> dp(2, vector<int64_t>(n + 1));
  dp[0][0] = dp[1][0] = 0;
  dp[0][1] = first[0];
  dp[1][1] = second[0];

  for (int i = 2; i <= n; ++i) {
    dp[0][i] = max(first[i - 1] + dp[1][i - 1], first[i - 1] + dp[1][i - 2]);
    dp[1][i] = max(second[i - 1] + dp[0][i - 1], second[i - 1] + dp[0][i - 2]);
  }
  std::cout << max(dp[0][n], dp[1][n]) << "\n";

  return 0;
}
