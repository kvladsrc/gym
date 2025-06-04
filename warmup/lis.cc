#include <algorithm>
#include <climits>
#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::string;
using std::vector;

namespace {

int upper_bound(vector<int> &a, int el) {
  int l = 0;
  int r = a.size() - 1;
  int res = -1;

  while (l <= r) {
    auto mid = (l + r) / 2;
    if (a[mid] >= el) {
      r = mid - 1;
    } else {
      res = mid;
      l = mid + 1;
    }
  }

  return res;
}

static int lis(vector<int> &a) {
  // EDGE_CASE: empty array.
  if (a.empty()) {
    return 0;
  }

  vector<int> dp(a.size(), INT_MAX);
  int res = 1;

  for (auto i : a) {
    auto best = upper_bound(dp, i);
    dp.front() = std::min(dp.front(), i);

    if (best == -1) {
      continue;
    }

    dp[best + 1] = std::min(dp[best + 1], i);
    res = std::max(res, best + 2);
  }

  return res;
}

}  // namespace

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  std::cout << lis(a) << "\n";

  return 0;
}
