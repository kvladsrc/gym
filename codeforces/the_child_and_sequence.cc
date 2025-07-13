#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
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

void build_tree(const vector<int> &sum, vector<int64_t> &tree,
                vector<int> &maximal, int v, int l, int r) {
  if (l == r) {
    tree[v] = sum[l];
    maximal[v] = sum[l];
    return;
  }

  auto m = (l + r) / 2;
  build_tree(sum, tree, maximal, v * 2, l, m);
  build_tree(sum, tree, maximal, (v * 2) + 1, m + 1, r);
  tree[v] = tree[v * 2] + tree[(v * 2) + 1];
  maximal[v] = max(maximal[v * 2], maximal[(v * 2) + 1]);
}

int64_t query_tree(const vector<int64_t> &tree, int v, int l, int r, int cl,
                   int cr) {
  if (cl > r || cr < l) {
    return 0;
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  auto m = (cl + cr) / 2;
  return query_tree(tree, v * 2, l, r, cl, m) +
         query_tree(tree, (v * 2) + 1, l, r, m + 1, cr);
}

void update_tree_2(vector<int64_t> &tree, vector<int> &maximal, int v, int l,
                   int r, int cl, int cr, int x) {
  if (cl > r || cr < l || maximal[v] < x) {
    return;
  }

  if (cl == cr) {
    maximal[v] = tree[v] = tree[v] % x;
    return;
  }

  auto m = (cl + cr) / 2;
  update_tree_2(tree, maximal, v * 2, l, r, cl, m, x);
  update_tree_2(tree, maximal, (v * 2) + 1, l, r, m + 1, cr, x);
  tree[v] = tree[v * 2] + tree[(v * 2) + 1];
  maximal[v] = max(maximal[v * 2], maximal[(v * 2) + 1]);
}

void update_tree_3(vector<int64_t> &tree, vector<int> &maximal, int v, int l,
                   int cl, int cr, int x) {
  if (l > cr || l < cl) {
    return;
  }

  if (cl == cr) {
    maximal[v] = tree[v] = x;
    return;
  }

  auto m = (cl + cr) / 2;
  update_tree_3(tree, maximal, v * 2, l, cl, m, x);
  update_tree_3(tree, maximal, (v * 2) + 1, l, m + 1, cr, x);
  tree[v] = tree[v * 2] + tree[(v * 2) + 1];
  maximal[v] = max(maximal[v * 2], maximal[(v * 2) + 1]);
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  int size = 1;
  while (size < n) {
    size *= 2;
  }
  size *= 2;

  vector<int64_t> sum(size);
  vector<int> maximal(size);
  build_tree(a, sum, maximal, 1, 0, n - 1);

  for (int i = 0; i < m; ++i) {
    int type = 0;
    int l = 0;
    int r = 0;
    int x = 0;
    int64_t q = 0;
    std::cin >> type;

    switch (type) {
      case 1:
        std::cin >> l >> r;
        q = query_tree(sum, 1, l - 1, r - 1, 0, n - 1);
        std::cout << q << "\n";
        break;
      case 2:
        std::cin >> l >> r >> x;
        update_tree_2(sum, maximal, 1, l - 1, r - 1, 0, n - 1, x);
        break;
      default:
        std::cin >> l >> x;
        update_tree_3(sum, maximal, 1, l - 1, 0, n - 1, x);
    }
  }

  return 0;
}
