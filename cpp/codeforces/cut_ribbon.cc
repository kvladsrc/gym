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

int cut_ribbon(int n, int a, int b, int c) {
  vector<int> dp(n + 1, -1);
  dp[0] = 0;

  for (int i = 1; i <= n; ++i) {
    int case1 = -1, case2 = -1, case3 = -1;
    if ((i - a) >= 0) case1 = dp[i - a];
    if ((i - b) >= 0) case2 = dp[i - b];
    if ((i - c) >= 0) case3 = dp[i - c];
    int best = max(max(case1, case2), case3);
    if (best != -1) best++;
    dp[i] = best;
  }

  return dp[n];
}

int main(int argc, char* argv[]) {
  int n, a, b, c;
  std::cin >> n >> a >> b >> c;
  std::cout << cut_ribbon(n, a, b, c) << "\n";

  return 0;
}
