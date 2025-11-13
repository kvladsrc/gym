#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <new>
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

int c_to_i(char c) { return c - 'a'; }

using alphabet = vector<int>;

struct node {
  alphabet chars;

  node *l;
  node *r;

  node() : chars(26, 0), l(nullptr), r(nullptr) {}

  ~node() {
    delete l;
    delete r;
  }

  alphabet add(node *other) {
    alphabet res(26, 0);
    for (size_t idx = 0; idx < 26; ++idx) {
      res[idx] = chars[idx] + other->chars[idx];
    }
    return res;
  }
};

node *build_tree(string &s, int left, int right) {
  auto res = new node();

  if (left == right) {
    res->chars[c_to_i(s[left])] = 1;
    return res;
  }

  auto mid = (left + right) / 2;
  res->l = build_tree(s, left, mid);
  res->r = build_tree(s, mid + 1, right);
  res->chars = res->l->add(res->r);

  return res;
}

void update_tree(node *root, int left, int right, int pos, char c) {
  if (pos < left || pos > right) {
    return;
  }

  if (left == right) {
    std::fill(root->chars.begin(), root->chars.end(), 0);
    root->chars[c_to_i(c)] = 1;
    return;
  }

  auto mid = (left + right) / 2;
  update_tree(root->l, left, mid, pos, c);
  update_tree(root->r, mid + 1, right, pos, c);

  root->chars = root->l->add(root->r);
}

alphabet query(node *root, int left, int right, int qleft, int qright) {
  alphabet res(26, 0);

  if (qright < left || qleft > right) {
    return res;
  }

  if (qleft <= left && right <= qright) {
    return root->chars;
  }

  auto mid = (left + right) / 2;
  auto lq = query(root->l, left, mid, qleft, qright);
  auto rq = query(root->r, mid + 1, right, qleft, qright);

  for (size_t idx = 0; idx < 26; ++idx) {
    res[idx] = lq[idx] + rq[idx];
  }
  return res;
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  auto *ft = build_tree(s, 0, s.size() - 1);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int op_type = 0;
    std::cin >> op_type;

    if (op_type == 1) {
      int pos = 0;
      std::cin >> pos;

      pos--;

      char c = 'a';
      std::cin >> c;

      update_tree(ft, 0, s.size() - 1, pos, c);
    } else {
      int l = 0;
      int r = 0;
      std::cin >> l >> r;

      l--;
      r--;

      auto res = query(ft, 0, s.size() - 1, l, r);
      int d =
          std::count_if(res.begin(), res.end(), [](int i) { return i > 0; });
      std::cout << d << "\n";
    }
  }

  return 0;
}
