#include <algorithm>
#include <iostream>
#include <set>
#include <vector>

using std::set;
using std::vector;

namespace {

/*
 * Theorem: Eulerian cycle exists if graph is connected and each vertex
 * has a even number of edges.
 */

using graph = vector<set<int>>;

void dfs_cc(const graph& g, vector<int>& cc_table, int start, int cc) {
  if (cc_table[start] != 0) {
    return;
  }
  cc_table[start] = cc;
  for (auto n : g[start]) {
    dfs_cc(g, cc_table, n, cc);
  }
}

bool connected(const graph& g) {
  vector<int> cc_table(g.size(), 0);
  const int init_cc = 1;
  dfs_cc(g, cc_table, 0, 1);

  return std::all_of(cc_table.begin(), cc_table.end(),
                     [init_cc](const auto& cc) { return cc == init_cc; });
}

bool all_even(const graph& g) {
  return std::all_of(g.begin(), g.end(),
                     [](const auto& v) { return v.size() % 2 == 0; });
}

bool eulerian_cycle_exists(const graph& g) {
  // EDGE_CASE: Empty graph has Eulerian path.
  if (g.empty()) {
    return true;
  }
  return connected(g) && all_even(g);
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  graph g(n);

  int e = 0;
  std::cin >> e;

  for (int i = 0; i < e; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    g[a].insert(b);
    g[b].insert(a);
  }

  std::cout << (eulerian_cycle_exists(g) ? "YES" : "NO") << "\n";

  return 0;
}
