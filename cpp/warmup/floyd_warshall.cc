#include "cpp/warmup/floyd_warshall.hpp"

#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <vector>

namespace warmup {

using adj_matrix = std::vector<std::vector<int>>;

void floyd_warshall(adj_matrix& dists) {
  std::size_t n = dists.size();

  for (std::size_t k = 0; k < n; ++k) {
    for (std::size_t a = 0; a < n; ++a) {
      for (std::size_t b = 0; b < n; ++b) {
        if (dists[a][k] != INT_MAX && dists[k][b] != INT_MAX) {
          dists[a][b] = std::min(dists[a][b], dists[a][k] + dists[k][b]);
        }
      }
    }
  }
}

}  // namespace warmup
