#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
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

const int modulo = 1000000007;

int rec_solve_start(int n, int k, int first) {
  if (k == 1) {
    return 1;
  }

  int64_t res = 0;
  for (int i = 1; i * first <= n; ++i) {
    res += rec_solve_start(n, k - 1, first * i);
    res %= modulo;
  }
  return static_cast<int>(res);
}

int rec_solve(int n, int k) {
  int64_t res = 0;
  for (int first = 1; first <= n; ++first) {
    res += rec_solve_start(n, k, first);
    res %= modulo;
  }
  return static_cast<int>(res);
}

int dp_solve(int n, int k) {

  // n + 1 and k + 1 is just for clearance, skipping i - 1 to address
  // array.
  vector<vector<int>> dp(n + 1, vector<int>(k + 1));

  for (int first = 1; first <= n; first++) {
    dp[first][1] = 1;
  }

  for (int inc_k = 2; inc_k <= k; ++inc_k) {
    for (int first = 1; first <= n; ++first) {
      for (int i = 1; (first * i) <= n; ++i) {
        dp[first][inc_k] += dp[first * i][inc_k - 1];
        dp[first][inc_k] %= modulo;
      }
    }
  }

  int res = 0;
  for (int i = 1; i <= n; ++i) {
    res += dp[i][k];
    res %= modulo;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int n, k;
  std::cin >> n >> k;

  // let a -- solution for f(n, k - 1) and first element is x, we can
  // find a solution for f(n, k) by multipliyng the solution by every
  // divider of x.
  //
  // Another aproach is to look for a solution for subproblems with
  // smaller n, k.

  std::cout << dp_solve(n, k) << "\n";

  return 0;
}
