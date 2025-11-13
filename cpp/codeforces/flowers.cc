#include <sys/types.h>

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const unsigned int modulo = 1000000007;

int variants_by_one(int n, int k, unordered_map<int, int> &dp) {
  if (n < k)
    // empty seq only
    return 1;

  if (dp[n]) return dp[n];

  uint64_t res = variants_by_one(n - 1, k, dp) + variants_by_one(n - k, k, dp);
  res %= modulo;

  int casted = static_cast<int>(res);
  dp[n] = casted;
  return casted;
}

int main(int argc, char *argv[]) {
  int t, k;
  std::cin >> t >> k;

  unordered_map<int, int> dp;

  vector<uint64_t> pref_sum(100000 + 1);
  pref_sum[0] = 0;
  for (int i = 1; i <= 100000; ++i) {
    pref_sum[i] = pref_sum[i - 1] + variants_by_one(i, k, dp);
  }

  for (int i = 0; i < t; ++i) {
    int l, r;
    std::cin >> l >> r;
    auto res = (pref_sum[r] - pref_sum[l - 1]) % modulo;
    std::cout << res << "\n";
  }

  return 0;
}
