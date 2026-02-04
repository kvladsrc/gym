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

int gcd(int a, int b) { return (b == 0 ? abs(a) : gcd(b, a % b)); }

void build_tree(vector<int>& tree, const vector<int>& sum, int v, int l,
                int r) {
  if (l == r) {
    tree[v] = sum[l];
    return;
  }

  auto m = (l + r) / 2;
  build_tree(tree, sum, v * 2, l, m);
  build_tree(tree, sum, (v * 2) + 1, m + 1, r);
  tree[v] = gcd(tree[v * 2], tree[v * 2 + 1]);
}

int query_tree(vector<int>& tree, int v, int l, int r, int cl, int cr) {
  if (l > cr || r < cl) {
    return 0;
  }

  if (cl >= l && cr <= r) {
    return tree[v];
  }

  auto m = (cl + cr) / 2;
  auto left = query_tree(tree, v * 2, l, r, cl, m);
  auto right = query_tree(tree, v * 2 + 1, l, r, m + 1, cr);
  return gcd(left, right);
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;
  vector<int> a(n);
  map<int, vector<size_t>> m;
  for (size_t idx = 0; idx < n; ++idx) {
    std::cin >> a[idx];
    m[a[idx]].push_back(idx);
  }

  int size = 1;
  while (size <= static_cast<int>(n)) {
    size *= 2;
  }
  size *= 2;

  vector<int> tree(size);
  build_tree(tree, a, 1, 0, n - 1);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int l = 0;
    int r = 0;
    std::cin >> l >> r;
    l--;
    r--;
    auto g = query_tree(tree, 1, l, r, 0, n - 1);
    auto lower = std::lower_bound(m[g].begin(), m[g].end(), l);
    auto upper = std::upper_bound(m[g].begin(), m[g].end(), r);
    int winners = upper - lower;
    std::cout << r - l + 1 - winners << "\n";
  }

  return 0;
}
