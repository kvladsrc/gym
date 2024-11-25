#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct node {
  vector<int> a_chars;
  vector<int> b_chars;
  node *l, *r;

  node() : a_chars(26, 0), b_chars(26, 0) {}

  int count_diff();
};

int node::count_diff() {
  int res = 0;
  for (int i = 0; i < 26; ++i) {
    res += abs(min(0, b_chars[i] - a_chars[i]));
  }
  return res;
}

void merge(node *l, node *r, node *root) {
  root->l = l;
  root->r = r;
  for (int i = 0; i < 26; ++i) {
    root->a_chars[i] += l->a_chars[i] + r->a_chars[i];
    root->b_chars[i] += l->b_chars[i] + r->b_chars[i];
  }
}

node query(int l, int r, int ql, int qr, node *root) {
  if (l > qr || r < ql) {
    return node();
  }

  if (l >= ql && r <= qr) {
    return *root;
  }

  int mid = (l + r) / 2;
  auto left_child = query(l, mid, ql, qr, root->l);
  auto right_child = query(mid + 1, r, ql, qr, root->r);

  auto res = node();
  merge(&left_child, &right_child, &res);

  return res;
}

void build_tree(string &a, string &b, int l, int r, node *root) {
  if (l > r) {
    return;
  }

  if (l == r) {
    root->a_chars[a[l] - 'a']++;
    root->b_chars[b[l] - 'a']++;
    return;
  }

  int mid = (l + r) / 2;
  auto left_child = new node;
  auto right_child = new node;
  build_tree(a, b, l, mid, left_child);
  build_tree(a, b, mid + 1, r, right_child);
  merge(left_child, right_child, root);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, q;
    std::cin >> n >> q;
    string a, b;
    std::cin >> a >> b;

    auto root = new node;
    build_tree(a, b, 0, n - 1, root);

    while (q--) {
      int l, r;
      std::cin >> l >> r;

      auto resp = query(0, n - 1, l - 1, r - 1, root);

      std::cout << resp.count_diff() << "\n";
    }
  }

  return 0;
}
