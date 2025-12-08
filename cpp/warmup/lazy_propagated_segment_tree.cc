#include "cpp/warmup/lazy_propagated_segment_tree.hpp"

#include <cstdint>
#include <vector>

using std::vector;

namespace warmup {

LazySegmentTree::LazySegmentTree(int n) : n_(n) {
  // 4 * n size is often used.
  int size = 1;
  while (size <= n) {
    size *= 2;
  }
  size *= 2;
  tree_.resize(size);
  lazy_.resize(size, 0);
}

void LazySegmentTree::build(const vector<int64_t>& sum) {
  build_tree(1, 0, n_ - 1, sum);
}

void LazySegmentTree::update(int l, int r) { update_tree(1, 0, n_ - 1, l, r); }

int64_t LazySegmentTree::query(int l, int r) {
  return query_tree(1, 0, n_ - 1, l, r);
}

void LazySegmentTree::build_tree(int v, int l, int r,
                                 const vector<int64_t>& sum) {
  if (l == r) {
    tree_[v] = sum[l];
    return;
  }

  int m = (l + r) / 2;
  build_tree(2 * v, l, m, sum);
  build_tree((2 * v) + 1, m + 1, r, sum);
  tree_[v] = tree_[2 * v] + tree_[(2 * v) + 1];
}

void LazySegmentTree::push(int v, int l, int r) {
  if (lazy_[v] == 0) {
    return;
  }

  if (l != r) {
    lazy_[2 * v] += lazy_[v];
    lazy_[(2 * v) + 1] += lazy_[v];
    if ((lazy_[v] % 2) != 0) {
      int m = (l + r) / 2;
      tree_[2 * v] = m - l + 1 - tree_[2 * v];
      tree_[(2 * v) + 1] = r - (m + 1) + 1 - tree_[(2 * v) + 1];
    }
  }

  lazy_[v] = 0;
}

void LazySegmentTree::update_tree(int v, int cl, int cr, int l, int r) {
  if (l > cr || r < cl) {
    return;
  }

  if (cl >= l && cr <= r) {
    tree_[v] = cr - cl + 1 - tree_[v];
    lazy_[v]++;
    return;
  }

  push(v, cl, cr);
  int m = (cl + cr) / 2;
  update_tree(2 * v, cl, m, l, r);
  update_tree((2 * v) + 1, m + 1, cr, l, r);
  tree_[v] = tree_[2 * v] + tree_[(2 * v) + 1];
}

int64_t LazySegmentTree::query_tree(int v, int cl, int cr, int l, int r) {
  if (l > cr || r < cl) {
    return 0;
  }

  if (cl >= l && cr <= r) {
    return tree_[v];
  }

  push(v, cl, cr);

  int m = (cl + cr) / 2;
  return query_tree(2 * v, cl, m, l, r) +
         query_tree((2 * v) + 1, m + 1, cr, l, r);
}

}  // namespace warmup
