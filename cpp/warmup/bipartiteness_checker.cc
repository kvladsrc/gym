#include "cpp/warmup/bipartiteness_checker.hpp"

#include <algorithm>
#include <cstddef>
#include <vector>

using std::vector;

namespace warmup {

using graph = vector<vector<int>>;

enum Color { None = 0, Red = 1, Blue = 2 };

bool bipartiteness_cc(const graph &g, vector<Color> &colors, std::size_t start,
                      Color cur_color) {
  if (colors[start] == cur_color) {
    return true;
  }

  if (colors[start] != 0) {
    return false;
  }

  colors[start] = cur_color;
  Color next_color = (cur_color == Red ? Blue : Red);

  if (std::any_of(std::begin(g[start]), std::end(g[start]), [&](auto n) {
        return !bipartiteness_cc(g, colors, n, next_color);
      })) {
    return false;
  }

  return true;
}

bool bipartiteness_test(const graph &g) {
  vector<Color> colors(g.size(), None);

  for (std::size_t v = 0; v < g.size(); ++v) {
    if (colors[v] == 0) {
      if (!bipartiteness_cc(g, colors, v, Red)) {
        return false;
      }
    }
  }

  return true;
}

}  // namespace warmup
