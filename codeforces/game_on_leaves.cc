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

typedef vector<vector<int>> graph;

int deg(int start, int from, graph &g) {
  int res = 1;
  for (auto v : g[start]) {
    if (v == from)
      continue;
    res += deg(v, start, g);
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, x;
    std::cin >> n >> x;

    graph g(n);
    for (int i = 1; i < n; ++i) {
      int a, b;
      std::cin >> a >> b;

      g[a - 1].push_back(b - 1);
      g[b - 1].push_back(a - 1);
    }

    // We interested only in degrees of vertecies connected to X, we
    // do not care about the structure of subtrees. Basicaly we are
    // interested in parity of number of connected vertecies with odd
    // degrees.

    int i = 0;
    for (auto v : g[x - 1]) {
      i += deg(v, x - 1, g) % 2;
    }

    if (i % 2 || g[x - 1].size() <= 1) {
      std::cout << "Ayush" << "\n";
    } else {
      std::cout << "Ashish" << "\n";
    }
  }

  return 0;
}
