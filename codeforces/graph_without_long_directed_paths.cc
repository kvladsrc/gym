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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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
using edge = pair<int, int>;

bool bipartiteness(graph &g, vector<int> &colors, int color, int start) {
  if (colors[start] == color) {
    return true;
  }

  if (colors[start] != 0) {
    return false;
  }

  colors[start] = color;
  auto ncolor = (color == 1 ? 2 : 1);

  if (std::any_of(std::begin(g[start]), std::end(g[start]), [&](auto n) {
        return !bipartiteness(g, colors, ncolor, n);
      })) {
    return false;
  }

  return true;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  graph g(n);
  vector<edge> edges(m);

  for (int v = 0; v < m; ++v) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
    edges[v] = {a, b};
  }

  vector<int> colors(n, 0);
  if (!bipartiteness(g, colors, 1, 0)) {
    std::cout << "NO" << "\n";
    return 0;
  }

  std::cout << "YES" << "\n";
  for (auto v : edges) {
    if (colors[v.first] == 1) {
      std::cout << 1;
    } else {
      std::cout << 0;
    }
  }
  std::cout << "\n";

  return 0;
}
