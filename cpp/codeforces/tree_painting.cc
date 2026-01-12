#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

using graph = vector<vector<int64_t>>;

void dfs_sizes(graph& g, vector<int64_t>& sizes, int v, int p) {
  sizes[v] = 1;
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    dfs_sizes(g, sizes, n, v);
    sizes[v] += sizes[n];
  }
}

void dfs_dp(graph& g, vector<int64_t>& dp, vector<int64_t>& sizes, int v,
            int p) {
  dp[v] = sizes[v];
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    dfs_dp(g, dp, sizes, n, v);
    dp[v] += dp[n];
  }
}

void dfs_dp2(graph& g, vector<int64_t>& dp2, vector<int64_t>& sizes, int v,
             int p) {
  dp2[v] = dp2[p];
  if (v != p) {
    dp2[v] -= sizes[v];
    dp2[v] += sizes.size() - sizes[v];
  }
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    dfs_dp2(g, dp2, sizes, n, v);
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  graph g(n);
  for (int i = 1; i < n; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  vector<int64_t> sizes(n);
  dfs_sizes(g, sizes, 0, 0);

  vector<int64_t> dp(n);
  dfs_dp(g, dp, sizes, 0, 0);

  vector<int64_t> dp2(n);
  dp2[0] = dp[0];
  dfs_dp2(g, dp2, sizes, 0, 0);

  int64_t res = std::accumulate(dp2.begin(), dp2.end(), 0,
                                [](int acc, int h) { return max(acc, h); });

  std::cout << res << "\n";

  return 0;
}
