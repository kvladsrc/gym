#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

bool bipartite_component(vector<int> &colors,
                         unordered_map<int, vector<size_t>> &pos_by_color,
                         vector<pair<int, int>> &a, size_t start, int color) {
  if (colors[start] == color) {
    return true;
  }

  if (colors[start] != 0) {
    return false;
  }

  if (a[start].first == a[start].second) {
    return false;
  }

  colors[start] = color;
  auto n_color = (color == 1 ? 2 : 1);

  for (auto n : pos_by_color[a[start].first]) {
    if (n == start) {
      continue;
    }

    if (!bipartite_component(colors, pos_by_color, a, n, n_color)) {
      return false;
    }
  }

  for (auto n : pos_by_color[a[start].second]) {
    if (n == start) {
      continue;
    }

    if (!bipartite_component(colors, pos_by_color, a, n, n_color)) {
      return false;
    }
  }

  return true;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    vector<pair<int, int>> a(n);
    unordered_map<int, vector<size_t>> pos_by_color;

    // 0 -- unset, 1 -- first, 2 -- second set.
    vector<int> colors(n, 0);

    for (size_t idx = 0; idx < n; ++idx) {
      std::cin >> a[idx].first >> a[idx].second;
      pos_by_color[a[idx].first].push_back(idx);
      pos_by_color[a[idx].second].push_back(idx);
    }

    bool res = true;
    for (size_t idx = 0; idx < n; ++idx) {
      if (colors[idx] == 0) {
        if (!bipartite_component(colors, pos_by_color, a, idx, 1)) {
          res = false;
          break;
        }
      }
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
