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
#include <optional>
#include <queue>
#include <set>
#include <sstream>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

void build_tree(vector<int64_t>& tree, const vector<int64_t>& sum, int v, int l,
                int r) {
  if (l == r) {
    tree[v] = sum[l];
    return;
  }

  auto m = (l + r) / 2;
  build_tree(tree, sum, v * 2, l, m);
  build_tree(tree, sum, (v * 2) + 1, m + 1, r);
  tree[v] = min(tree[v * 2], tree[(v * 2) + 1]);
}

void push(vector<int64_t>& tree, vector<int64_t>& lazy, int v, int l, int r) {
  if (l == r) {
    lazy[v] = 0;
    return;
  }

  lazy[v * 2] += lazy[v];
  lazy[(v * 2) + 1] += lazy[v];
  tree[v * 2] += lazy[v];
  tree[(v * 2) + 1] += lazy[v];
  lazy[v] = 0;
}

void update_tree(vector<int64_t>& tree, vector<int64_t>& lazy, int v, int l,
                 int r, int cl, int cr, int64_t x) {
  if (cl > r || cr < l) {
    return;
  }

  if (cl >= l && cr <= r) {
    tree[v] += x;
    lazy[v] += x;
    return;
  }

  push(tree, lazy, v, cl, cr);
  auto m = (cl + cr) / 2;
  update_tree(tree, lazy, v * 2, l, r, cl, m, x);
  update_tree(tree, lazy, (v * 2) + 1, l, r, m + 1, cr, x);
  tree[v] = min(tree[v * 2], tree[(v * 2) + 1]);
}

int64_t query_tree(vector<int64_t>& tree, vector<int64_t>& lazy, int v, int l,
                   int r, int cl, int cr) {
  if (cl > r || cr < l) {
    return INT64_MAX;
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  push(tree, lazy, v, cl, cr);
  auto m = (cl + cr) / 2;
  auto left = query_tree(tree, lazy, v * 2, l, r, cl, m);
  auto right = query_tree(tree, lazy, (v * 2) + 1, l, r, m + 1, cr);
  return min(left, right);
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  int size = 1;
  while (size <= n) {
    size *= 2;
  }
  size *= 2;

  vector<int64_t> tree(size);
  vector<int64_t> lazy(size, 0);
  build_tree(tree, a, 1, 0, n - 1);

  int m = 0;
  std::cin >> m;
  string line;
  std::cin.ignore();

  for (int i = 0; i < m; ++i) {
    std::getline(std::cin, line);
    std::stringstream ss(line);

    int l = 0;
    int r = 0;
    int64_t v = 0;
    ss >> l >> r;

    if (ss >> v) {
      if (l > r) {
        update_tree(tree, lazy, 1, l, n - 1, 0, n - 1, v);
        update_tree(tree, lazy, 1, 0, r, 0, n - 1, v);
      } else {
        update_tree(tree, lazy, 1, l, r, 0, n - 1, v);
      }

    } else {
      int64_t Q = INT64_MAX;
      if (l > r) {
        Q = min(query_tree(tree, lazy, 1, l, n - 1, 0, n - 1), Q);
        Q = min(query_tree(tree, lazy, 1, 0, r, 0, n - 1), Q);
      } else {
        Q = query_tree(tree, lazy, 1, l, r, 0, n - 1);
      }

      std::cout << Q << "\n";
    }
  }

  return 0;
}
