#include "cpp/warmup/graph_topological_sort.hpp"

#include <cstddef>
#include <vector>

namespace warmup {

void dfs(const graph& g, std::size_t start, int& clock,
         std::vector<int>& rank) {
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

void topological_sort(const graph& g, std::vector<int>& rank) {
  int clock = 0;

  for (std::size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

}  // namespace warmup
