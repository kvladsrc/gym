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

  // Searching for shortest path from 0 to all other verticies.
  // Init dists.
  vector<int> dists(n);
  dists[0] = 0;
  for (int i = 1; i < n; ++i) {
    dists[i] = graph[0][i];
  }

  // Let's go!
  for (int iter = 0; iter < n; ++iter) {
    for (int to = 1; to < n; ++to) {
      for (int by = 1; by < n; ++by) {
        if (to == by)
          continue;
        if (dists[to] > (dists[by] + graph[by][to])) {
          dists[to] = (dists[by] + graph[by][to]);
        }
      }
    }
  }

  for (int i = 1; i < n; ++i) {
    std::cout << "0 to " << i << " dist: " << dists[i] << "\n";
  }

  return 0;
}
