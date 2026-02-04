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

int solve_rec(string& s, size_t start, vector<int>& table) {
  if (start >= s.size()) {
    return 0;
  }

  if (table[start] != -1) {
    return table[start];
  }

  int case1 = INT_MAX;
  size_t idx = s.find(s[start], start + 1);
  if (idx != string::npos) {
    case1 = idx - (start + 1);
    case1 += solve_rec(s, idx + 1, table);
  }

  int case2 = solve_rec(s, start + 1, table) + 1;

  table[start] = min(case1, case2);

  return table[start];
}

int solve_dp(const string& s) {
  vector<int> dp(s.size() + 1, INT_MAX);
  dp.front() = 0;

  map<char, size_t> last_seen;

  for (size_t idx = 1; idx <= s.size(); ++idx) {
    int case1 = dp[idx - 1] + 1;

    int case2 = INT_MAX;
    if (last_seen[s[idx - 1]]) {
      case2 = (idx - last_seen[s[idx - 1]]) - 1;
      case2 += dp[last_seen[s[idx - 1]] - 1];
    }

    last_seen[s[idx - 1]] = idx;

    dp[idx] = min(case1, case2);
  }

  return dp.back();
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;

    std::cout << solve_dp(s) << "\n";
  }

  return 0;
}
