#include "cpp/warmup/centroid.hpp"

#include <cstddef>
#include <vector>

namespace warmup {

int fill_sizes(const graph& g, std::vector<int>& s, size_t v, size_t p) {
  s[v] = 1;
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    s[v] += fill_sizes(g, s, n, v);
  }
  return s[v];
}

size_t centroid(const graph& g, const std::vector<int>& sizes, size_t v,
                size_t p) {
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }

    if (sizes[n] > (static_cast<int>(g.size()) / 2)) {
      return centroid(g, sizes, n, v);
    }
  }
  return v;
}

}  // namespace warmup
