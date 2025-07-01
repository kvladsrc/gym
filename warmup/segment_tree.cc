#include "cpp/warmup/segment_tree.hpp"

#include <algorithm>
#include <climits>
#include <string>
#include <vector>

namespace warmup {

meta::meta() : minimal(INT_MAX), maximal(INT_MIN) {}

meta::meta(int mn, int mx) : minimal(mn), maximal(mx) {}

meta meta::operator+(const meta &other) const {
  return {std::min(minimal, other.minimal), std::max(maximal, other.maximal)};
}

meta query(node *root, int ql, int qr, int l, int r) {
  if (ql <= l && qr >= r) {
    return root->m;
  }

  if (qr < l || ql > r) {
    return {INT_MAX, INT_MIN};
  }

  auto mid = (l + r) / 2;
  auto left = query(root->left, ql, qr, l, mid);
  auto right = query(root->right, ql, qr, mid + 1, r);

  return left + right;
}

node *build_tree(std::vector<int> &a, int start, int end) {
  auto *res = new node;

  if (start == end) {
    res->m.maximal = a[start];
    res->m.minimal = a[start];
    return res;
  }

  auto mid = (start + end) / 2;
  res->left = build_tree(a, start, mid);
  res->right = build_tree(a, mid + 1, end);
  res->m = res->left->m + res->right->m;

  return res;
}

}  // namespace warmup
