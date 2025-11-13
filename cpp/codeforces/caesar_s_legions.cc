#include <algorithm>
#include <climits>
#include <cmath>
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
#include <utility>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const int64_t modulo = 100000000;

int64_t solve(bool f_pref, int n1, int n2, int k1, int k2) {
  if (n1 < 0 || n2 < 0) {
    return 0;
  }

  if (n1 == 0 && n2 == 0) {
    return 1;
  }

  int64_t res = 0;
  if (f_pref) {
    for (int n = 1; n <= k2; ++n) {
      res += solve(false, n1, n2 - n, k1, k2);
      res %= modulo;
    }
  } else {
    for (int n = 1; n <= k1; ++n) {
      res += solve(true, n1 - n, n2, k1, k2);
      res %= modulo;
    }
  }
  return res;
}

pair<int64_t, int64_t> solve_dp(int n1, int n2, int k1, int k2) {
  // el.first -- seqs starting from footmans; el.seconds -- starts by
  // horsemans.
  vector<vector<pair<int64_t, int64_t>>> dp(
      n1 + 1, vector<pair<int64_t, int64_t>>(n2 + 1));

  dp[0][0] = {1, 1};

  for (int f = 1; f <= n1; ++f) {
    if (f <= k1) {
      dp[f][0] = {1, 0};
    } else {
      dp[f][0] = {0, 0};
    }
  }

  for (int h = 1; h <= n2; ++h) {
    if (h <= k2) {
      dp[0][h] = {0, 1};
    } else {
      dp[0][h] = {0, 0};
    }
  }

  for (int footmans = 1; footmans <= n1; ++footmans) {
    for (int horsemans = 1; horsemans <= n2; ++horsemans) {
      int64_t ffrst = 0;
      for (int n = 1; n <= k1; ++n) {
        if (footmans - n < 0) {
          break;
        }

        ffrst += dp[footmans - n][horsemans].second;
        ffrst %= modulo;
      }

      int64_t hfrst = 0;
      for (int n = 1; n <= k2; ++n) {
        if (horsemans - n < 0) {
          break;
        }
        hfrst += dp[footmans][horsemans - n].first;
        hfrst %= modulo;
      }

      dp[footmans][horsemans] = {ffrst, hfrst};
    }
  }

  return dp.back().back();
}

int main(int argc, char *argv[]) {
  int n1, n2, k1, k2;
  std::cin >> n1 >> n2 >> k1 >> k2;

  auto res = solve_dp(n1, n2, k1, k2);
  std::cout << (res.first + res.second) % modulo << "\n";

  return 0;
}
