#include <algorithm>
#include <climits>
#include <iostream>
#include <string>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::string;
using std::vector;

struct meta {
  int minimal{};
  int maximal{};

  meta() = default;

  meta(int mn, int mx) : minimal(mn), maximal(mx) {}

  meta operator+(const meta &other) const {
    return {min(minimal, other.minimal), max(maximal, other.maximal)};
  }
};

struct node {
  meta m;
  node *left{};
  node *right{};
};

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

static node *build_tree(vector<int> &a, int start, int end) {
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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  if (n == 0) {
    return 0;
  }

  auto *root = build_tree(a, 0, n - 1);

  int m = 0;
  std::cin >> m;
  while ((m--) != 0) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;
    l--;
    r--;
    auto resp = query(root, l, r, 0, n - 1);
    std::cout << resp.maximal << "\n";
    std::cout << resp.minimal << "\n";
  }

  return 0;
}
