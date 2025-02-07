#include <algorithm>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::string;
using std::vector;

// Can be optimized by binary search. Current implementation has
// O(n^2) runtime complexity.
static int lis(vector<int> &a) {
  vector<int> dp(a.size() + 1);
  dp.front() = 0;
  int res = 0;

  for (size_t idx = 0; idx < a.size(); ++idx) {
    dp[idx + 1] = 1;
    for (size_t prev = 0; prev < idx; ++prev) {
      if (a[prev] < a[idx]) {
        dp[idx + 1] = max(dp[idx + 1], dp[prev + 1] + 1);
      }
    }

    res = max(res, dp[idx + 1]);
  }

  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << lis(a) << "\n";

  return 0;
}
