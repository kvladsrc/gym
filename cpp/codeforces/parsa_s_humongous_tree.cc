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

using graph = vector<vector<int>>;

pair<int64_t, int64_t> solve(graph& g, int start, int p, vector<int>& l,
                             vector<int>& r,
                             vector<pair<int64_t, int64_t>>& table) {
  if (table[start].first != -1) {
    return table[start];
  }

  table[start] = {0, 0};

  for (auto n : g[start]) {
    if (n == p) {
      continue;
    }

    auto con = solve(g, n, start, l, r, table);
    auto lcase1 = con.first + abs(l[start] - l[n]);
    auto lcase2 = con.second + abs(l[start] - r[n]);
    auto rcase1 = con.first + abs(r[start] - l[n]);
    auto rcase2 = con.second + abs(r[start] - r[n]);
    table[start].first += max(lcase1, lcase2);
    table[start].second += max(rcase1, rcase2);
  }

  return table[start];
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    vector<int> l(n);
    vector<int> r(n);

    for (int i = 0; i < n; ++i) {
      std::cin >> l[i];
      std::cin >> r[i];
    }

    graph g(n);
    for (int i = 1; i < n; ++i) {
      int a = 0;
      int b = 0;
      std::cin >> a >> b;
      a--;
      b--;
      g[a].push_back(b);
      g[b].push_back(a);
    }

    // v.first is the maximum res if v is l[v] and v.second if v =
    // r[v];
    vector<pair<int64_t, int64_t>> table(n, {-1, -1});
    auto res = solve(g, 0, 0, l, r, table);
    std::cout << max(res.first, res.second) << "\n";
  }

  return 0;
}
