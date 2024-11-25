#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
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
using std::map;
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

int lis(vector<int> &a) {
  vector<int> dp(a.size() + 1);
  dp.front() = 0;

  int mx = 0;
  for (size_t idx = 1; idx <= a.size(); ++idx) {
    int idx_local = 0;
    for (size_t i = 1; i < idx; ++i) {
      if (a[idx_local - 1] <= a[idx]) {
        if (dp[idx_local] < dp[i]) {
          idx_local = i;
        }
      }
    }

    dp[idx] = dp[idx_local] + 1;

    mx = max(mx, dp[idx]);
  }

  return mx;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << lis(a) << "\n";

  return 0;
}
