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

// 0 is for empty, 1 is for conneted, 2 is denied.
using graph = vector<vector<int>>;

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, m;
  std::cin >> n >> m;

  // Dunno. Brute force! Start with vertex with minimal denied edges,
  // add all allowed edges. Then for every denied vertex do the
  // following: add all possible, then add edges for the rest
  // vertexies to added. It always possible, otherwise we cannot
  // construct the path with two edges.
  graph g(n, vector<int>(n, 0));
  vector<int> num_denied(n, 0);
  while (m--) {
    int a, b;
    std::cin >> a >> b;
    a--;
    b--;
    g[a][b] = 2;
    g[b][a] = 2;
    num_denied[a]++;
    num_denied[b]++;
  }

  int best = 0;
  for (int a = 0; a < n; ++a) {
    if (num_denied[a] < num_denied[best]) {
      best = a;
    }
  }

  queue<int> next;
  for (int to = 0; to < n; ++to) {
    if (best == to) {
      continue;
    }

    if (g[best][to] == 2) {
      next.push(to);
    }

    g[best][to] = 1;
    g[to][best] = 1;
  }

  while (!next.empty()) {
    auto v = next.front();
    next.pop();

    queue<int> edge_case;

    for (int to = 0; to < n; ++to) {
      if (v == to) {
        continue;
      }

      if (g[v][to] == 2) {
        edge_case.push(to);
      }

      g[v][to] = 1;
      g[to][v] = 1;
    }

    while (!edge_case.empty()) {
      auto ev = next.front();
      next.pop();

      for (int to = 0; to < n; ++to) {
        if (g[v][to] != 1 || g[ev][to] == 2) {
          continue;
        }
        g[ev][to] = 1;
        break;
      }
    }
  }

  int res = 0;
  for (auto r : g) {
    for (auto e : r) {
      if (e == 1) {
        res++;
      }
    }
  }
  res /= 2;
  std::cout << res << "\n";
  for (int a = 0; a < n; ++a) {
    for (int b = a + 1; b < n; ++b) {
      if (g[a][b] == 1) {
        std::cout << a + 1 << " " << b + 1 << "\n";
      }
    }
  }

  return 0;
}
