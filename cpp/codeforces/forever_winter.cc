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

using graph = vector<vector<int>>;

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, m;
    std::cin >> n >> m;
    graph g(n);
    while (m--) {
      int a, b;
      std::cin >> a >> b;
      g[a - 1].push_back(b - 1);
      g[b - 1].push_back(a - 1);
    }

    map<int, int> table;
    for (const auto& v : g) {
      table[v.size()]++;
    }

    // X only one vertex contain X edges. Edge case: x == y + 1;
    int x = -1, y = -1;
    for (auto p : table) {
      // Leaf vertex.
      if (p.first == 1) {
        continue;
      }

      // Unique x;
      if (p.second == 1) {
        x = p.first;
        continue;
      }

      y = p.first;
    }

    if (x == -1) {
      x = y;
    }
    y--;

    std::cout << x << " " << y << "\n";
  }

  return 0;
}
