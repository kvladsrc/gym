#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <exception>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <utility>
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

int solve_rec(vector<int>& a, bool you_turn, size_t start) {
  if (start >= a.size()) {
    return 0;
  }

  int case1 = solve_rec(a, !you_turn, start + 1);
  int case2 = solve_rec(a, !you_turn, start + 2);
  if (!you_turn) {
    if (a[start] == 1) {
      case1++;
      case2++;
    }

    if (a[start + 1] == 1) {
      case2++;
    }
  }

  return min(case1, case2);
}

int solve_dp(const vector<int>& a) {
  // p.first if for you, p.second is for friend.
  vector<pair<int, int>> dp(a.size() + 1);
  dp[0].first = 0;
  dp[0].second = 0;

  for (size_t i = 1; i <= a.size(); ++i) {
    dp[i].first = dp[i - 1].second;
    dp[i].second = dp[i - 1].first;
    if (a[a.size() - i] == 1) {
      dp[i].second++;
    }

    if (i >= 2) {
      dp[i].first = min(dp[i].first, dp[i - 2].second);

      int case2 = dp[i - 2].first;
      if (a[a.size() - i] == 1) {
        case2++;
      }
      if (a[a.size() - (i - 1)] == 1) {
        case2++;
      }
      dp[i].second = min(dp[i].second, case2);
    }
  }

  return dp.back().second;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    std::cout << solve_dp(a) << "\n";
  }

  return 0;
}
