#include <algorithm>
#include <climits>
#include <cmath>
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

typedef vector<vector<int>> graph;

void dfs_cc(graph& g, int cc, int start, vector<int>& cc_table,
            map<int, vector<int>>& cc_map) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;
  cc_map[cc].push_back(start);
  for (auto v : g[start]) {
    dfs_cc(g, cc, v, cc_table, cc_map);
  }
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;
  graph g(n);
  while (m--) {
    int a, b;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  // Idea is to find all connected components and then check each
  // component if it is cycle by condition: each vertes in cc has 2
  // edges.
  vector<int> cc_table(n, 0);
  map<int, vector<int>> cc_map;
  int cc = 1;
  for (int i = 0; i < n; ++i) {
    if (cc_table[i] == 0) {
      dfs_cc(g, cc++, i, cc_table, cc_map);
    }
  }

  int res = 0;
  for (int i = 1; i < cc; ++i) {
    bool is_cycle = std::all_of(cc_map[i].begin(), cc_map[i].end(),
                                [&g](int v) { return g[v].size() == 2; });

    if (is_cycle) {
      res++;
    }
  }

  std::cout << res << "\n";

  return 0;
}
