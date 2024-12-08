#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using graph = vector<vector<int>>;

void cc_dfs(graph &g, int v, vector<int> &cc_table, int cc) {
  if (cc_table[v]) {
    return;
  }

  cc_table[v] = cc;
  for (auto nv : g[v]) {
    cc_dfs(g, nv, cc_table, cc);
  }
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int v, e;
  std::cin >> v >> e;
  graph g(v);
  vector<int> cc_table(v, 0);
  while (e--) {
    int a, b;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  cc_dfs(g, 0, cc_table, 1);
  for (int nv = 0; nv < v; ++nv) {
    if (cc_table[nv]) {
      std::cout << nv + 1 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}
