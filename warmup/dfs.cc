#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

using graph = vector<vector<int>>;

static void cc_dfs(graph &g, int v, vector<int> &cc_table, int cc) {
  if (cc_table[v] != 0) {
    return;
  }

  cc_table[v] = cc;
  for (auto nv : g[v]) {
    cc_dfs(g, nv, cc_table, cc);
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int v = 0;
  int e = 0;
  std::cin >> v >> e;
  graph g(v);
  vector<int> cc_table(v, 0);
  while ((e--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  cc_dfs(g, 0, cc_table, 1);
  for (int nv = 0; nv < v; ++nv) {
    if (cc_table[nv] != 0) {
      std::cout << nv + 1 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
