#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// Minimal number of changes to get string b from a.
int edit_distance(string &a, string &b) {
  // Let x = LCS(a, b), then we need to add len(b) - len(x) and remove
  // len(a) - len(x) some chars. That is not optimal, because we can
  // reduce amount of operations by replacing some characters.
  //
  // So we implementing dp by 3 cases (incert, delete, replace char
  // from a);
  vector<vector<int>> dp(a.size() + 1, vector<int>(b.size() + 1));
  for (size_t i = 0; i <= a.size(); ++i) {
    // Remove all.
    dp[i][0] = i;
  }
  for (size_t i = 0; i <= b.size(); ++i) {
    // Add all to empty.
    dp[0][i] = i;
  }

  for (size_t a_i = 1; a_i <= a.size(); ++a_i) {
    for (size_t b_i = 1; b_i <= b.size(); ++b_i) {
      if (a[a_i - 1] == b[b_i - 1]) {
        dp[a_i][b_i] = dp[a_i - 1][b_i - 1];
        continue;
      }

      // case_delete = 1 + ed(a.begin() + 1, b);
      // case_replace = 1 + ed(a.begin() + 1, b.begin() + 1);
      // case_incert = 1 + ed(a, b.begin() + 1);
      int case_delete = 1 + dp[a_i - 1][b_i];
      int case_replace = 1 + dp[a_i - 1][b_i - 1];
      int case_incert = 1 + dp[a_i][b_i - 1];
      dp[a_i][b_i] = min(case_delete, min(case_replace, case_incert));
    }
  }

  return dp.back().back();
}

int main(int argc, char *argv[]) {
  string a, b;
  std::cin >> a >> b;
  std::cout << edit_distance(a, b) << "\n";

  return 0;
}