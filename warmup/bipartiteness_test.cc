#include <cstddef>
#include <iostream>
#include <vector>

using std::vector;

using graph = vector<vector<int>>;

bool bipartiteness_cc(graph &g, vector<int> &colors, size_t start, int color) {
  if (colors[start] == color) {
    return true;
  }

  if (colors[start] != 0) {
    return false;
  }

  colors[start] = color;
  auto n_color = (color == 1 ? 2 : 1);

  for (auto n : g[start]) {
    if (!bipartiteness_cc(g, colors, n, n_color)) {
      return false;
    }
  }

  return true;
}

bool bipartiteness_test(graph &g) {
  vector<int> colors(g.size(), 0);

  for (size_t v = 0; v < g.size(); ++v) {
    if (colors[v] == 0) {
      if (!bipartiteness_cc(g, colors, v, 1)) {
        return false;
      }
    }
  }

  return true;
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  graph g(n);

  int k = 0;
  std::cin >> k;

  while ((k--) > 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  std::cout << (bipartiteness_test(g) ? "YES" : "NO") << "\n";

  return 0;
}
