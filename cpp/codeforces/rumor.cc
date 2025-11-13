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

int dfs(vector<vector<int>> &g, int start, int comp_id, vector<int> &comp_ids,
        vector<int> &costs) {
  if (comp_ids[start] != 0) return costs[start];

  comp_ids[start] = comp_id;
  int cheapest = costs[start];
  for (auto n : g[start]) {
    auto cost = dfs(g, n, comp_id, comp_ids, costs);
    cheapest = min(cheapest, cost);
  }

  return cheapest;
}

int main(int argc, char *argv[]) {
  int n, m;
  std::cin >> n >> m;
  vector<int> costs(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> costs[i];
  }

  vector<vector<int>> g(n);

  while (m--) {
    int a, b;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  vector<int> comp_ids(n, 0);
  int cur_comp = 1;
  int64_t res = 0;
  for (int i = 0; i < n; ++i) {
    if (comp_ids[i] == 0) {
      res += dfs(g, i, cur_comp++, comp_ids, costs);
    }
  }

  std::cout << res << "\n";

  return 0;
}
