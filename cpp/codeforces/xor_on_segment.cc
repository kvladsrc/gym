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
#include <stack>
#include <stdexcept>
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

void build_tree(vector<int64_t> &tree, const vector<int64_t> &sum, int v, int l,
                int r) {
  if (l == r) {
    tree[v] = sum[l];
    return;
  }

  int m = (l + r) / 2;
  build_tree(tree, sum, 2 * v, l, m);
  build_tree(tree, sum, (2 * v) + 1, m + 1, r);
  tree[v] = tree[2 * v] + tree[(2 * v) + 1];
}

void push(vector<int64_t> &tree, vector<int64_t> &lazy, int v, int l, int r) {
  if (lazy[v] == 0) {
    return;
  }

  if (l != r) {
    lazy[2 * v] += lazy[v];
    lazy[(2 * v) + 1] += lazy[v];
    if ((lazy[v] % 2) != 0) {
      int m = (l + r) / 2;
      tree[2 * v] = m - l + 1 - tree[2 * v];
      tree[(2 * v) + 1] = r - (m + 1) + 1 - tree[(2 * v) + 1];
    }
  }

  lazy[v] = 0;
}

void update_tree(vector<int64_t> &tree, vector<int64_t> &lazy, int v, int l,
                 int r, int cl, int cr) {
  if (l > cr || r < cl) {
    return;
  }

  if (cl >= l && cr <= r) {
    tree[v] = cr - cl + 1 - tree[v];
    lazy[v]++;
    return;
  }

  push(tree, lazy, v, cl, cr);
  int m = (cl + cr) / 2;
  update_tree(tree, lazy, 2 * v, l, r, cl, m);
  update_tree(tree, lazy, (2 * v) + 1, l, r, m + 1, cr);
  tree[v] = tree[2 * v] + tree[(2 * v) + 1];
}

int64_t query_tree(vector<int64_t> &tree, vector<int64_t> &lazy, int v, int l,
                   int r, int cl, int cr) {
  if (l > cr || r < cl) {
    return 0;
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  push(tree, lazy, v, cl, cr);

  int m = (cl + cr) / 2;
  return query_tree(tree, lazy, 2 * v, l, r, cl, m) +
         query_tree(tree, lazy, (2 * v) + 1, l, r, m + 1, cr);
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  int size = 1;
  while (size <= n) {
    size *= 2;
  }
  size *= 2;

  vector<vector<int64_t>> a(20, vector<int64_t>(n));
  vector<vector<int64_t>> trees(20, vector<int64_t>(size));
  vector<vector<int64_t>> lazy(20, vector<int64_t>(size, 0));

  for (int i = 0; i < n; ++i) {
    int64_t buf = 0;
    std::cin >> buf;
    for (int j = 0; j < 20; ++j) {
      a[j][i] = buf % 2;
      buf /= 2;
    }
  }

  for (auto i = 0; i < 20; ++i) {
    build_tree(trees[i], a[i], 1, 0, n - 1);
  }

  int m = 0;
  std::cin >> m;
  for (int i = 0; i < m; ++i) {
    int type = 0;
    int l = 0;
    int r = 0;
    std::cin >> type >> l >> r;

    if (type == 1) {
      int64_t res = 0;
      int64_t power = 0;
      for (int row = 0; row < 20; ++row) {
        auto q = query_tree(trees[row], lazy[row], 1, l - 1, r - 1, 0, n - 1);
        res += q << power++;
      }
      std::cout << res << "\n";
      continue;
    }

    int x = 0;
    std::cin >> x;
    for (int row = 0; row < 20; ++row) {
      if ((x % 2) != 0) {
        update_tree(trees[row], lazy[row], 1, l - 1, r - 1, 0, n - 1);
      }
      x /= 2;
    }
  }

  return 0;
}
