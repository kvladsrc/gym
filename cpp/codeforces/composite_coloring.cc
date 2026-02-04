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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int gcd(int a, int b) {
  if (a % b == 0) {
    return b;
  }
  return gcd(b, a % b);
}

using graph = vector<vector<int>>;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    graph g(n);

    for (size_t i = 0; i < static_cast<size_t>(n); ++i) {
      for (size_t j = i + 1; j < static_cast<size_t>(n); ++j) {
        if (gcd(a[i], a[j]) == 1) {
          g[i].push_back(j);
          g[j].push_back(i);
        }
      }
    }

    map<int, int> color_map;
    int max_degree = 0;
    for (int i = 0; i < n; ++i) {
      if (g[i].size() > g[max_degree].size()) {
        max_degree = i;
      }
    }

    int max_color = 1;
    color_map[max_degree] = 1;
    vector<set<int>> colored_deg(n);
    for (auto neighbor : g[max_degree]) {
      colored_deg[neighbor].insert(1);
    }

    for (int i = 1; i < n; ++i) {
      int best_v = -1;
      for (int v = 0; v < n; ++v) {
        if (color_map[v] != 0) {
          continue;
        }

        if (best_v == -1 ||
            colored_deg[best_v].size() < colored_deg[v].size()) {
          best_v = v;
        }
      }

      if (best_v == -1) {
        break;
      }

      for (int color = 1;; ++color) {
        bool can_be = std::none_of(g[best_v].begin(), g[best_v].end(),
                                   [&color_map, color](int neighbor) {
                                     return color_map[neighbor] == color;
                                   });
        if (can_be) {
          color_map[best_v] = color;
          max_color = max(max_color, color);
          for (auto neighbor : g[best_v]) {
            colored_deg[neighbor].insert(color);
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
  }

  return 0;
}
