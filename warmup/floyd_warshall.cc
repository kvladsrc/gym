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

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  const int inf = 1000000000;
  vector<vector<int>> graph(n, vector<int>(n, inf));

  int e;
  std::cin >> e;
  while (e--) {
    int a, b, d;
    std::cin >> a >> b >> d;
    graph[a][b] = d;
  }

  // Init by copying graph.
  vector<vector<int>> dists(graph);

  // Searching for a shortests pathes between any two verticies.
  for (int iter = 0; iter < n; ++iter) {
    for (int from = 0; from < n; ++from) {
      for (int to = 0; to < n; ++to) {
        if (to == from)
          continue;
        for (int by = 0; by < n; ++by) {
          if (by == to || by == from) {
            continue;
          }

          if (dists[from][to] > (dists[from][by] + dists[by][to])) {
            dists[from][to] = dists[from][by] + dists[by][to];
          }
        }
      }
    }
  }

  for (int i = 1; i < n; ++i) {
    std::cout << "0 to " << i << " dist: " << dists[0][i] << "\n";
  }

  return 0;
}
