#include <climits>
#include <cstddef>
#include <iostream>
#include <queue>
#include <utility>
#include <vector>

using std::pair;
using std::priority_queue;
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

void dfs_cc(graph &g, int start, int cc, vector<int> &cc_table) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;

  for (auto n : g[start]) {
    dfs_cc(g, n, cc, cc_table);
  }
}

void topological_sort(graph &g, vector<int> &rank) {
  int clock = 0;

  for (std::size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

// Kosaraju's algorithm.
int main(int /*argc*/, char * /*argv*/[]) {
  std::size_t v = 0;
  int e = 0;
  std::cin >> v >> e;

  vector<vector<int>> g(v);
  vector<vector<int>> g_reversed(v);

  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;

    g[a].push_back(b);
    g_reversed[b].push_back(a);
  }

  vector<int> rank(v, 0);
  topological_sort(g_reversed, rank);

  class CompareDistance {
   public:
    bool operator()(const pair<int, int> &p1, const pair<int, int> &p2) const {
      return p1.first < p2.first;
    }
  };

  priority_queue<pair<int, int>, vector<pair<int, int>>, CompareDistance>
      max_first_pair_heap;

  for (std::size_t idx = 0; idx < v; ++idx) {
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

  for (std::size_t idx = 0; idx < v; ++idx) {
    std::cout << idx << " " << cc_table[idx] << "\n";
  }

  return 0;
}
