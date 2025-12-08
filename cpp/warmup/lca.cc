#include "cpp/warmup/lca.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <utility>
#include <vector>

namespace warmup {

meta::meta() : d(INT_MAX), idx(0) {}

meta::meta(int ad, std::size_t aidx) : d(ad), idx(aidx) {}

meta meta::operator+(const meta& other) const {
  if (d < other.d) {
    return {d, idx};
  }
  return {other.d, other.idx};
}

lca::lca(const graph& g) {
  if (g.empty()) {
    return;
  }

  pre.resize(g.size());
  dists.resize(g.size());
  dists[0] = 0;
  ett(g, 0, 0);
  tree.resize(path.size() * 4);
  build_tree(0, path.size() - 1, 1);
}

void lca::ett(const graph& g, std::size_t v, std::size_t p) {
  dists[v] = dists[p] + 1;
  pre[v] = path.size();
  path.push_back(v);
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    ett(g, n, v);
    path.push_back(v);
  }
}

void lca::build_tree(std::size_t l, std::size_t r, std::size_t v) {
  if (l == r) {
    tree[v].d = dists[path[l]];
    tree[v].idx = path[l];
    return;
  }

  auto m = (l + r) / 2;
  build_tree(l, m, v * 2);
  build_tree(m + 1, r, (v * 2) + 1);

  tree[v] = tree[v * 2] + tree[(v * 2) + 1];
}

meta lca::query_tree(std::size_t l, std::size_t r, std::size_t cl,
                     std::size_t cr, std::size_t v) {
  if (cl > r || cr < l) {
    return {INT_MAX, 0};
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  auto m = (cl + cr) / 2;
  return query_tree(l, r, cl, m, v * 2) +
         query_tree(l, r, m + 1, cr, (v * 2) + 1);
}

std::size_t lca::find(std::size_t a, std::size_t b) {
  if (pre.empty()) {
    return 0;
  }
  if (a >= pre.size() || b >= pre.size()) {
    return 0;
  }

  auto prea = pre[a];
  auto preb = pre[b];
  if (preb < prea) {
    std::swap(prea, preb);
  }

  auto q = query_tree(prea, preb, 0, path.size() - 1, 1);
  return q.idx;
}

}  // namespace warmup
