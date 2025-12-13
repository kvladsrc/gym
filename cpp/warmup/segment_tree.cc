#include "cpp/warmup/segment_tree.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <string>
#include <vector>

namespace warmup {

meta::meta() : minimal(INT_MAX), maximal(INT_MIN) {}

meta::meta(int mn, int mx) : minimal(mn), maximal(mx) {}

meta meta::operator+(const meta& other) const {
  return {std::min(minimal, other.minimal), std::max(maximal, other.maximal)};
}

segment_tree::segment_tree(const std::vector<int>& v) {
  if (v.empty()) {
    return;
  }

  tree.resize(v.size() * 4);
  build(0, v.size() - 1, 1, v);
}

void segment_tree::build(std::size_t l, std::size_t r, std::size_t v,
                         const std::vector<int>& vec) {
  if (l == r) {
    tree[v] = {vec[l], vec[l]};
    return;
  }

  auto m = (l + r) / 2;
  build(l, m, v * 2, vec);
  build(m + 1, r, (v * 2) + 1, vec);
  tree[v] = tree[v * 2] + tree[(v * 2) + 1];
}

meta segment_tree::query(std::size_t l, std::size_t r) {
  return query(0, (tree.size() / 4) - 1, l, r, 1);
}

meta segment_tree::query(std::size_t cl, std::size_t cr, std::size_t l,
                         std::size_t r, std::size_t v) {
  if (cl > r || cr < l) {
    return meta(INT_MAX, INT_MIN);
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  auto m = (cl + cr) / 2;
  return query(cl, m, l, r, v * 2) + query(m + 1, cr, l, r, (v * 2) + 1);
}

}  // namespace warmup
