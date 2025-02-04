#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
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

void topological_sort(graph &g, vector<int> &rank) {
  int clock = 0;

  for (size_t v = 0; v < g.size(); ++v) {
    dfs(g, v, clock, rank);
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  size_t v = 0;
  int e = 0;
  std::cin >> v >> e;

  graph g(v);

  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    g[a].push_back(b);
  }

  vector<int> rank(v, 0);
  topological_sort(g, rank);

  for (size_t idx = 0; idx < v; ++idx) {
    std::cout << idx << " " << rank[idx];
    std::cout << "\n";
  }

  return 0;
}
