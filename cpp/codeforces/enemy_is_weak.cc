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

struct meta {
  int64_t val = 0;
  meta() = default;
  explicit meta(int64_t aval) : val(aval) {}
  meta operator+(const meta& other) const { return meta(val + other.val); }
};

struct node {
  meta m{};
  node* l = nullptr;
  node* r = nullptr;

  node() = default;
};

void build_empty_tree(node* root, int64_t l, int64_t r) {
  if (l == r) {
    return;
  }

  auto mid = (l + r) / 2;
  root->l = new node();
  root->r = new node();

  build_empty_tree(root->l, l, mid);
  build_empty_tree(root->r, mid + 1, r);
}

void update_tree(node* root, int64_t l, int64_t r, int64_t pos) {
  root->m.val++;

  if (l == r) {
    return;
  }

  auto mid = (l + r) / 2;

  if (pos <= mid) {
    update_tree(root->l, l, mid, pos);
  } else {
    update_tree(root->r, mid + 1, r, pos);
  }
}

int64_t query_tree(node* root, int64_t l, int64_t r, int64_t pos) {
  if (l > pos) {
    return 0;
  }

  if (r <= pos) {
    return root->m.val;
  }

  auto mid = (l + r) / 2;
  return query_tree(root->l, l, mid, pos) +
         query_tree(root->r, mid + 1, r, pos);
}

int main(int /*argc*/, char* /*argv*/[]) {
  int64_t n = 0;
  std::cin >> n;
  map<int64_t, int64_t> m;
  for (int64_t i = 1; i <= n; ++i) {
    int64_t buf = 0;
    std::cin >> buf;
    m[buf] = i;
  }

  auto* root = new node();
  build_empty_tree(root, 1, n);
  int64_t res = 0;
  int64_t i = 0;
  for (auto p : m) {
    update_tree(root, 1, n, p.second);
    i++;

    if (p.second <= 1 || p.second >= n) {
      continue;
    }

    auto less_left = query_tree(root, 1, n, p.second - 1);
    auto greater_left = p.second - 1 - less_left;
    auto less_right = (i - 1) - less_left;
    res += greater_left * less_right;
  }

  std::cout << res << "\n";

  return 0;
}
