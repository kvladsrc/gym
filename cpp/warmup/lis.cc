#include "cpp/warmup/lis.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

namespace warmup {

int lis(vector<int>& a) {
  // EDGE_CASE: empty array.
  if (a.empty()) {
    return 0;
  }

  // dp[i] stores the smallest ending element of an increasing subsequence of
  // length i+1.
  vector<int> dp;
  dp.push_back(a[0]);

  for (std::size_t i = 1; i < a.size(); ++i) {
    if (a[i] > dp.back()) {
      dp.push_back(a[i]);
    } else {
      // Find the first element in dp that is greater than or equal to a[i]
      *std::lower_bound(dp.begin(), dp.end(), a[i]) = a[i];
    }
  }

  return dp.size();
}

}  // namespace warmup
