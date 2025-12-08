#include "cpp/warmup/ford_bellman.hpp"

#include <algorithm>
#include <vector>

using std::vector;

namespace warmup {

vector<int> ford_bellman(int n, const vector<vector<int>>& graph) {
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
        if (to == by) {
          continue;
        }
        dists[to] = std::min(dists[to], dists[by] + graph[by][to]);
      }
    }
  }

  return dists;
}

}  // namespace warmup
