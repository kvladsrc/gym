#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

using graph = std::vector<std::vector<std::size_t>>;

class lca {
 public:
  explicit lca(const graph& g);
  int find_dist(std::size_t a, std::size_t b);

 private:
  std::vector<int> path;
  std::vector<std::size_t> pre;
  std::vector<vector<int>> st;
  std::vector<int> logs;

  void ett(const graph& g, std::size_t v, std::size_t p, int d);
  void build_st();
  int query_st(std::size_t l, std::size_t r);
};

lca::lca(const graph& g) {
  if (g.empty()) {
    return;
  }

  pre.resize(g.size());
  ett(g, 0, 0, 1);
  logs.resize(path.size() + 1);
  build_st();
}

void lca::ett(const graph& g, std::size_t v, std::size_t p, int d) {
  pre[v] = path.size();
  path.push_back(d);
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    ett(g, n, v, d + 1);
    path.push_back(d);
  }
}

void lca::build_st() {
  st.emplace_back(path);

  int window = 2;
  int cur_power = 1;

  while (window <= path.size()) {
    st.emplace_back();
    st.back().resize(path.size() - window + 1);

    for (size_t i = 0; i + window - 1 < path.size(); ++i) {
      st[cur_power][i] =
          min(st[cur_power - 1][i], st[cur_power - 1][i + (window / 2)]);
    }
    window *= 2;
    cur_power++;
  }

  logs[0] = 0;
  logs[1] = 0;
  for (size_t i = 2; i < logs.size(); ++i) {
    logs[i] = logs[i / 2] + 1;
  }
}

int lca::query_st(std::size_t l, std::size_t r) {
  auto power = logs[r - l + 1];
  return min(st[power][l], st[power][r - (1 << power) + 1]);
}

int lca::find_dist(std::size_t a, std::size_t b) {
  if (pre.empty()) {
    return 0;
  }
  if (a >= pre.size() || b >= pre.size()) {
    return 0;
  }

  auto prea = pre[a];
  auto preb = pre[b];
  if (preb < prea) {
    std::swap(prea, preb);
  }

  auto pd = query_st(prea, preb);
  return (path[prea] - pd) + (path[preb] - pd);
}

void closest_red(const graph& g, std::vector<int>& d,
                 const std::vector<size_t>& red) {
  std::queue<std::size_t> q;

  for (auto r : red) {
    q.push(r);
    d[r] = 0;
  }

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();

    for (auto adj : g[cur]) {
      if (d[adj] <= d[cur] + 1) {
        continue;
      }
      d[adj] = d[cur] + 1;
      q.push(adj);
    }
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  std::cin >> n >> m;

  graph g(n);
  for (int i = 1; i < n; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;

    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  vector<size_t> red;
  red.push_back(0);
  vector<int> dists(n, INT_MAX);
  closest_red(g, dists, red);
  vector<size_t> cur(m);
  size_t cur_idx = 0;
  lca solver(g);

  for (int i = 0; i < m; ++i) {
    int t = 0;
    int v = 0;
    std::cin >> t >> v;

    v--;

    if (t == 1) {
      red.push_back(v);
      if (cur_idx * cur_idx < m) {
        cur[cur_idx++] = v;
      } else {
        closest_red(g, dists, red);
        cur_idx = 0;
      }
    } else {
      int res = dists[v];
      for (size_t idx = 0; idx < cur_idx; ++idx) {
        auto cv = cur[idx];
        auto p = solver.find_dist(cv, v);
        res = min(res, p);
      }
      std::cout << res << "\n";
    }
  }

  return 0;
}
