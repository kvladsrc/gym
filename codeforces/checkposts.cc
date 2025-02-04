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

using graph = vector<vector<int>>;

void dfs(graph &g, int start, int &clock, vector<int> &rank) {
  if (rank[start] != 0) {
    return;
  }

  // Visited.
  rank[start] = -1;

  clock++;
  for (auto n : g[start]) {
    dfs(g, n, clock, rank);
  }

  rank[start] = clock++;
}

void dfs_cc(graph &g, int start, int cc, vector<int> &cc_table,
            vector<int> &cc_list) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;
  cc_list.push_back(start);

  for (auto n : g[start]) {
    dfs_cc(g, n, cc, cc_table, cc_list);
  }
}

void topological_sort(graph &g, vector<int> &rank) {
  int clock = 0;

  for (size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;
  vector<int> costs(n);
  for (auto &i : costs) {
    std::cin >> i;
  }

  int m = 0;
  std::cin >> m;
  graph g(n);
  graph rg(n);
  for (int i = 0; i < m; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    rg[b].push_back(a);
  }

  /*

    1. Use Kosaraju's algorithm to find CCs.

    2. For every CC find a minimal cost for building checkpost C and
    CNUM (number of vertecies with minimal cost).

    3. Answer is C1 + C2 + ... + Cn and CNUM1 * CNUM2 * ... * CNUMn.

  */

  vector<int> rank(n, 0);
  topological_sort(rg, rank);

  class CompareDistance {
   public:
    bool operator()(const pair<int, int> &p1, const pair<int, int> &p2) const {
      return p1.first < p2.first;
    }
  };

  priority_queue<pair<int, int>, vector<pair<int, int>>, CompareDistance>
      max_first_pair_heap;

  for (size_t idx = 0; idx < n; ++idx) {
    pair<int, int> r = {rank[idx], idx};
    max_first_pair_heap.push(r);
  }

  int cc = 1;
  vector<int> cc_table(n, 0);
  const int64_t modulo = 1000000007;
  int64_t fres = 0;
  int64_t sres = 1;

  while (!max_first_pair_heap.empty()) {
    auto best = max_first_pair_heap.top();
    max_first_pair_heap.pop();

    if (cc_table[best.second] != 0) {
      continue;
    }

    vector<int> cc_list;
    dfs_cc(g, best.second, cc, cc_table, cc_list);

    int best_cost = INT_MAX;
    int best_count = 0;

    for (auto idx : cc_list) {
      if (best_cost > costs[idx]) {
        best_cost = costs[idx];
        best_count = 1;
      } else if (best_cost == costs[idx]) {
        best_count++;
      }
    }

    fres += best_cost;
    sres = (sres * best_count) % modulo;
    cc++;
  }

  std::cout << fres << " " << sres << "\n";

  return 0;
}
