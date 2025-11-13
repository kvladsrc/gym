#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// TODO(zjgkkn): Use Kadane's algorithm.
int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int> in(n);
  int total = 0;
  for (int i = 0; i < n; ++i) {
    std::cin >> in[i];
    total += in[i];
  }

  vector<vector<int>> dp(n, vector<int>(n));
  int best_i = 0, best_j = 0;
  for (int i = 0; i < n; ++i) {
    dp[i][i] = in[i] ? -1 : 1;
    if (dp[i][i] > dp[best_i][best_j]) {
      best_i = i;
      best_j = i;
    }

    for (int j = i + 1; j < n; ++j) {
      dp[i][j] = dp[i][j - 1] + (in[j] ? -1 : 1);

      if (dp[i][j] > dp[best_i][best_j]) {
        best_i = i;
        best_j = j;
      }
    }
  }

  std::cout << total + dp[best_i][best_j] << "\n";

  return 0;
}
