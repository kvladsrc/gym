#include <boost/range/algorithm/sort.hpp>
#include <cstddef>
#include <iostream>
#include <vector>

using std::pair;
using std::vector;

// Asuming the dunning on DAG.
static void dfs(vector<vector<int>> &g, int &time, vector<int> &rank,
                int start) {
  // Because of running on DAG the value can be filled by previous
  // runs.
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

static void dfs_cc(vector<vector<int>> &g, vector<int> &cc_table, int start,
                   int cc) {
  if (cc_table[start] != 0) {
    return;
  }

  cc_table[start] = cc;
  for (auto i : g[start]) {
    dfs_cc(g, cc_table, i, cc);
  }
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

static bool compare_pair(const pair<int, int> &a, const pair<int, int> &b) {
  return a.second < b.second;
}

// Kosaraju's algorithm.
int main(int /*argc*/, char * /*argv*/[]) {
  int v = 0;
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

  auto table = topological_sort(g_reversed);
  vector<pair<int, int>> vertex_rank(g.size());
  for (int i = 0; i < v; ++i) {
    vertex_rank[i] = {i, table[i]};
  }

  boost::range::sort(vertex_rank, compare_pair);
  vector<int> cc_table(v, 0);
  int cc = 1;
  while (!vertex_rank.empty()) {
    auto cur = vertex_rank.back();
    vertex_rank.pop_back();
    if (cc_table[cur.first] != 0) {
      continue;
    }

    dfs_cc(g, cc_table, cur.first, cc++);
  }

  for (int i = 0; i < v; ++i) {
    std::cout << i << " " << cc_table[i] << "\n";
  }

  return 0;
}
