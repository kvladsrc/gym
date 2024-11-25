#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int cheap_travel_naive(int n, int m, int a, int b) {
  if (n <= 0) return 0;

  int case1 = cheap_travel_naive(n - 1, m, a, b) + a;
  int case2 = cheap_travel_naive(n - m, m, a, b) + b;

  return min(case1, case2);
}

int cheap_travel(int n, int m, int a, int b) {
  vector<int> dp(n + 1);
  for (int i = 1; i <= n; ++i) {
    int case1 = dp[i - 1] + a;
    int case2;
    if ((i - m) >= 0) {
      case2 = dp[i - m] + b;
    } else {
      case2 = b;
    }
    dp[i] = min(case1, case2);
  }
  return dp[n];
}

int main(int argc, char *argv[]) {
  int n, m, a, b;
  std::cin >> n >> m >> a >> b;
  std::cout << cheap_travel(n, m, a, b) << "\n";

  return 0;
}
