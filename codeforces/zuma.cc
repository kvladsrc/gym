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
using std::endl;
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

int solve(vector<int> &a, int start, int end, vector<vector<int>> &memo) {
  if (start == end) {
    return 1;
  }

  if (start > end) {
    return 0;
  }

  if (memo[start][end] != -1) {
    return memo[start][end];
  }

  for (int i = start + 1; i <= end; ++i) {
    if (memo[start][i] != -1) {
      continue;
    }

    memo[start][i] = memo[start][i - 1] + 1;
    for (int j = start; j < i; ++j) {
      if (a[j] == a[i]) {
        auto r = solve(a, start, j - 1, memo);

        int m = 1;
        if (j + 1 <= i - 1) {
          m = solve(a, j + 1, i - 1, memo);
        }

        memo[start][i] = min(memo[start][i], r + m);
      }
    }
  }

  return memo[start][end];
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  vector<vector<int>> memo(a.size(), vector<int>(a.size(), -1));
  for (size_t i = 0; i < n; ++i) {
    memo[i][i] = 1;
  }

  auto res = solve(a, 0, n - 1, memo);

  std::cout << res << "\n";

  return 0;
}
