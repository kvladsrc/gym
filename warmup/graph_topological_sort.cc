#include <cstddef>
#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

// Asuming the dunning on DAG.
static void dfs(vector<vector<int>> &g, int &time, vector<int> &rank,
                int start) {
  if (rank[start] != 0) {
    return;
  }

  time++;
  for (auto i : g[start]) {
    dfs(g, time, rank, i);
  }
  time++;
  rank[start] = time;
}

static vector<int> topological_sort(vector<vector<int>> &g) {
  vector<int> rank(g.size(), 0);
  int time = 0;
  for (size_t i = 0; i < rank.size(); ++i) {
    // Confusing part. Shall we keep results of previous run?
    if (rank[i] == 0) {
      dfs(g, time, rank, i);
    }
  }

  return rank;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int v = 0;
  int e = 0;
  std::cin >> v >> e;
  vector<vector<int>> g(v);
  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    g[a].push_back(b);
  }

  auto table = topological_sort(g);
  for (int i = 0; i < v; ++i) {
    std::cout << i << " " << table[i];
    std::cout << "\n";
  }

  return 0;
}
