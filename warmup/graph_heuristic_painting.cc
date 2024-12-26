#include <algorithm>
#include <iostream>
#include <map>
#include <set>
#include <string>
#include <vector>

using std::map;
using std::max;
using std::set;
using std::string;
using std::vector;

using graph = vector<vector<int>>;

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  graph g(n);
  while ((m--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  int max_degree = 0;
  map<int, int> color_map;
  for (int i = 0; i < n; ++i) {
    if (g[i].size() > g[max_degree].size()) {
      max_degree = i;
    }
  }

  int max_color = 1;
  color_map[max_degree] = 1;
  vector<set<int>> colored_deg(n);
  for (auto n : g[max_degree]) {
    colored_deg[n].insert(1);
  }

  for (int i = 1; i < n; ++i) {
    int best_v = -1;
    for (int v = 0; v < n; ++v) {
      if (color_map[v] != 0) {
        continue;
      }

      if (best_v == -1 || colored_deg[best_v].size() < colored_deg[v].size()) {
        best_v = v;
      }
    }

    for (int color = 1;; ++color) {
      bool can_be = true;
      for (auto n : g[best_v]) {
        if (color_map[n] == color) {
          can_be = false;
          break;
        }
      }
      if (can_be) {
        color_map[best_v] = color;
        max_color = max(max_color, color);
        for (auto n : g[best_v]) {
          colored_deg[n].insert(color);
        }
        break;
      }
    }
  }

  std::cout << max_color << "\n";
  for (int idx = 0; idx < n; ++idx) {
    std::cout << color_map[idx] << " ";
  }
  std::cout << "\n";

  return 0;
}
