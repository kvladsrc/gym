#include <iostream>
#include <queue>
#include <string>
#include <vector>

using std::queue;
using std::string;
using std::vector;

using graph = vector<vector<int>>;

static void cc_bfs(graph &g, int v, vector<int> &cc_table, int cc) {
  queue<int> q;
  q.push(v);
  while (!q.empty()) {
    auto nv = q.front();
    q.pop();

    if (cc_table[nv] != 0) {
      continue;
    }

    cc_table[nv] = cc;
    for (auto nnv : g[nv]) {
      q.push(nnv);
    }
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

  cc_bfs(g, 0, cc_table, 1);
  for (int nv = 0; nv < v; ++nv) {
    if (cc_table[nv] != 0) {
      std::cout << nv + 1 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
