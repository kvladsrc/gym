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

int rec_solve(vector<int> &s, int last_idx, int loc_idx) {
  if (last_idx != -1 && loc_idx % last_idx != 0) {
    return rec_solve(s, last_idx, loc_idx + 1);
  }

  if (static_cast<size_t>(loc_idx) > s.size()) {
    return 0;
  }

  if (last_idx != -1 && s[last_idx - 1] >= s[loc_idx - 1]) {
    return rec_solve(s, last_idx, loc_idx + 1);
  }

  int case1 = rec_solve(s, loc_idx, loc_idx + 1) + 1;
  int case2 = rec_solve(s, last_idx, loc_idx + 1);

  return max(case1, case2);
}

int dp_solve(vector<int> &s) {
  vector<int> dp(s.size());
  for (int idx = static_cast<int>(dp.size()) - 1; idx >= 0; --idx) {
    dp[idx] = 0;
    for (int i = 2; ((idx + 1) * i) <= static_cast<int>(dp.size()); ++i) {
      if (s[idx] < s[((idx + 1) * i) - 1]) {
        dp[idx] = max(dp[idx], dp[(idx + 1) * i - 1]);
      }
    }
    dp[idx]++;
  }

  int best = std::accumulate(dp.begin(), dp.end(), 0,
                             [](int acc, int i) { return max(acc, i); });
  return best;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> s(n);
    for (auto &i : s) {
      std::cin >> i;
    }

    std::cout << dp_solve(s) << "\n";
  }

  return 0;
}
