#include "cpp/warmup/graph_heuristic_painting.hpp"

#include <algorithm>
#include <map>
#include <set>
#include <vector>

using std::map;
using std::max;
using std::set;
using std::vector;

namespace warmup {

map<int, int> heuristic_painting(int n, const graph& g, int& max_color_out) {
  if (n == 0) {
    max_color_out = 0;
    return {};
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
  for (auto nv : g[max_degree]) {
    colored_deg[nv].insert(1);
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
      bool can_be =
          !std::any_of(g[best_v].begin(), g[best_v].end(),
                       [&](int nv) { return color_map[nv] == color; });
      if (can_be) {
        color_map[best_v] = color;
        max_color = max(max_color, color);
        for (auto nv : g[best_v]) {
          colored_deg[nv].insert(color);
        }
        break;
      }
    }
  }

  max_color_out = max_color;
  return color_map;
}

}  // namespace warmup
