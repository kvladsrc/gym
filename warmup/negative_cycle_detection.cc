#include "cpp/warmup/negative_cycle_detection.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <iostream>
#include <vector>

using std::min;
using std::vector;

namespace warmup {

using adj_matrix = vector<vector<int>>;

bool negative_cycle_detection(adj_matrix& dists) {
  std::size_t n = dists.size();

  // Floyd–Warshall algorithm.
  for (std::size_t k = 0; k < n; ++k) {
    for (std::size_t a = 0; a < n; ++a) {
      for (std::size_t b = 0; b < n; ++b) {
        if (dists[a][k] != INT_MAX && dists[k][b] != INT_MAX) {
          dists[a][b] = min(dists[a][b], dists[a][k] + dists[k][b]);
        }
      }
    }
  }

  bool found = false;
  for (std::size_t i = 0; i < n; ++i) {
    if (dists[i][i] < 0) {
      found = true;
      break;
    }
  }

  return found;
}

}  // namespace warmup
