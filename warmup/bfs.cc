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

void cc_bfs(graph &g, int v, vector<int> &cc_table, int cc) {
  queue<int> q;
  q.push(v);
  while (!q.empty()) {
    auto nv = q.front();
    q.pop();

    if (cc_table[nv]) {
      continue;
    }

    cc_table[nv] = cc;
    for (auto nnv : g[nv]) {
      q.push(nnv);
    }
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

  cc_bfs(g, 0, cc_table, 1);
  for (int nv = 0; nv < v; ++nv) {
    if (cc_table[nv]) {
      std::cout << nv + 1 << " ";
    }
  }
  std::cout << "\n";

  return 0;
}