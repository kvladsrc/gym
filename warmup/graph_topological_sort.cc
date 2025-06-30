#include "cpp/warmup/graph_topological_sort.hpp"

#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

namespace warmup {

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

void topological_sort(graph &g, vector<int> &rank) {
  int clock = 0;

  for (std::size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

}  // namespace warmup
