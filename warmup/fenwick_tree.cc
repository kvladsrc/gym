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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct node {
  int minimal;
  int maximal;
  node *left;
  node *right;
};

pair<int, int> query(node *root, int ql, int qr, int l, int r) {
  if (ql <= l && qr >= r) {
    return {root->maximal, root->minimal};
  }

  if (qr < l || ql > r) {
    return {INT_MIN, INT_MAX};
  }

  auto mid = (l + r) / 2;
  auto left_resp = query(root->left, ql, qr, l, mid);
  auto right_resp = query(root->right, ql, qr, mid + 1, r);
  return {max(left_resp.first, right_resp.first),
          min(left_resp.second, right_resp.second)};
}

node *build_tree(vector<int> &a, int start, int end) {
  auto res = new node;

  if (start == end) {
    res->maximal = a[start];
    res->minimal = a[start];
    return res;
  }

  auto mid = (start + end) / 2;
  res->left = build_tree(a, start, mid);
  res->right = build_tree(a, mid + 1, end);
  res->maximal = max(res->left->maximal, res->right->maximal);
  res->minimal = min(res->left->minimal, res->right->minimal);
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  if (n == 0) {
    return 0;
  }

  auto root = build_tree(a, 0, n - 1);

  int m;
  std::cin >> m;
  while (m--) {
    int l, r;
    std::cin >> l >> r;
    l--;
    r--;
    auto resp = query(root, l, r, 0, n - 1);
    std::cout << resp.first << "\n";
    std::cout << resp.second << "\n";
  }

  return 0;
}
