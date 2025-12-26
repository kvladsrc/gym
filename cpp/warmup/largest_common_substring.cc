#include "cpp/warmup/largest_common_substring.hpp"

#include <algorithm>
#include <string>
#include <vector>

using std::max;
using std::string;
using std::vector;

namespace warmup {

/*
  Finding largest common substring (any string that can be created by
  removing chars from beginning or end of the string) by DP algorithm by
  O(N * M) runtime. More effective algorithm exists, it uses a prefix
  trie.
*/

int largest_common_substring(const string& a, const string& b) {
  // No need to store table. Only previous result is needed.
  vector<vector<int>> dp(2, vector<int>(a.size() + 1, 0));

  int cur = 1;
  int res = 0;

  for (auto c : b) {
    for (size_t i = 1; i <= a.size(); ++i) {
      dp[cur][i] = 0;
      if (a[i - 1] == c) {
        dp[cur][i] = dp[1 - cur][i - 1] + 1;
      }
      res = max(res, dp[cur][i]);
    }
    cur = 1 - cur;
  }

  return res;
}

}  // namespace warmup
