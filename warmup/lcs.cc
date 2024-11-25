#include <algorithm>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

int lcs_dp(vector<int> &a, vector<int> &b) {
  vector<vector<int>> dp(a.size() + 1, vector<int>(b.size() + 1));

  for (size_t i = 0; i <= a.size(); ++i) {
    dp[i][0] = 0;
  }

  for (size_t i = 0; i <= b.size(); ++i) {
    dp[0][i] = 0;
  }

  for (size_t a_i = 1; a_i <= a.size(); ++a_i) {
    for (size_t b_i = 1; b_i <= b.size(); ++b_i) {
      if (a[a_i - 1] == b[b_i - 1]) {
        dp[a_i][b_i] = dp[a_i - 1][b_i - 1] + 1;
        continue;
      }

      int case1 = dp[a_i - 1][b_i];
      int case2 = dp[a_i][b_i - 1];
      dp[a_i][b_i] = max(case1, case2);
    }
  }

  return dp.back().back();
}

int lcs(vector<int> a, vector<int> b) {
  if (a.empty() || b.empty())
    return 0;

  vector<int> b_case(b.begin() + 1, b.end());
  vector<int> a_case(a.begin() + 1, a.end());

  if (a[0] == b[0])
    return lcs(a_case, b_case) + 1;

  return max(lcs(a, b_case), lcs(a_case, b));
}

int main() {
  size_t n;
  std::cin >> n;
  vector<int> a(n);
  for (size_t i = 0; i < n; i++) {
    std::cin >> a[i];
  }

  size_t m;
  std::cin >> m;
  vector<int> b(m);
  for (size_t i = 0; i < m; i++) {
    std::cin >> b[i];
  }

  std::cout << lcs(a, b) << std::endl;
}
