#include "cpp/warmup/strongly_connected_components.hpp"

#include <queue>
#include <utility>
#include <vector>

using std::pair;
using std::priority_queue;
using std::vector;

namespace warmup {

void dfs(const graph& g, int start, int& clock, vector<int>& rank) {
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

void dfs_cc(const graph& g, int start, int cc, vector<int>& cc_table) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;

  for (auto n : g[start]) {
    dfs_cc(g, n, cc, cc_table);
  }
}

void topological_sort(const graph& g, vector<int>& rank) {
  int clock = 0;

  for (std::size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

// Kosaraju's algorithm.
vector<int> find_scc(int v, const vector<pair<int, int>>& edges) {
  if (v == 0) return {};

  vector<vector<int>> g(v);
  vector<vector<int>> g_reversed(v);

  for (const auto& edge : edges) {
    g[edge.first].push_back(edge.second);
    g_reversed[edge.second].push_back(edge.first);
  }

  vector<int> rank(v, 0);
  topological_sort(g_reversed, rank);

  class CompareDistance {
   public:
    bool operator()(const pair<int, int>& p1, const pair<int, int>& p2) const {
      return p1.first < p2.first;
    }
  };

  priority_queue<pair<int, int>, vector<pair<int, int>>, CompareDistance>
      max_first_pair_heap;

  for (int idx = 0; idx < v; ++idx) {
    pair<int, int> r = {rank[idx], idx};
    max_first_pair_heap.push(r);
  }

  int cc = 1;
  vector<int> cc_table(v, 0);

  while (!max_first_pair_heap.empty()) {
    auto best = max_first_pair_heap.top();
    max_first_pair_heap.pop();

    if (cc_table[best.second] != 0) {
      continue;
    }

    dfs_cc(g, best.second, cc++, cc_table);
  }

  return cc_table;
}

}  // namespace warmup
