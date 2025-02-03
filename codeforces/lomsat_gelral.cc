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

size_t fill_p(graph &g, vector<int> &p) {
  queue<pair<int, int>> q;
  q.push({0, 0});
  set<int> unique_p;

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();
    p[cur.second] = cur.first;
    unique_p.insert(cur.first);
    for (auto n : g[cur.second]) {
      if (n != cur.first) {
        q.push({cur.second, n});
      }
    }
  }

  return unique_p.size();
}

void solve_case1(graph &g, vector<int64_t> &colors, vector<int> &p) {
  auto n = g.size();

  vector<map<int64_t, int64_t>> colors_in_subtree(n);
  vector<int64_t> sum(n);
  vector<int64_t> maximal(n, 0);

  for (size_t v = 0; v < n; ++v) {
    auto color = colors[v];
    auto node = v;
    while (true) {
      colors_in_subtree[node][color]++;

      if (colors_in_subtree[node][color] > maximal[node]) {
        maximal[node] = colors_in_subtree[node][color];
        sum[node] = color;
      } else if (colors_in_subtree[node][color] == maximal[node]) {
        sum[node] += color;
      }

      if (node == 0) {
        break;
      }

      node = p[node];
    }
  }

  for (size_t v = 0; v < n; ++v) {
    std::cout << sum[v] << " ";
  }
  std::cout << "\n";
}

void solve_case2(graph &g, vector<int64_t> &colors, vector<int> &p,
                 vector<int64_t> &sum,
                 vector<map<int64_t, int64_t>> &colors_in_subtree, size_t v,
                 map<int64_t, int> &unique_color) {
  map<int64_t, int> colors_cur;
  colors_cur[colors[v]]++;
  sum[v] = colors[v];
  int best = 1;

  for (auto n : g[v]) {
    if (n == p[v]) {
      continue;
    }

    solve_case2(g, colors, p, sum, colors_in_subtree, n, unique_color);

    for (auto c : colors_in_subtree[n]) {
      colors_cur[c.first] += c.second;
      if (colors_cur[c.first] > best) {
        sum[v] = c.first;
        best = colors_cur[c.first];
      } else if (colors_cur[c.first] == best) {
        sum[v] += c.first;
      }
    }
  }

  for (auto p : colors_cur) {
    if (best > unique_color[p.first]) {
      continue;
    }

    colors_in_subtree[v][p.first] = p.second;
  }
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;

  vector<int64_t> colors(n);
  map<int64_t, int> unique_color;
  for (auto &i : colors) {
    std::cin >> i;
    unique_color[i]++;
  }

  graph g(n);
  for (size_t e = 1; e < n; ++e) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  vector<int> p(n);
  fill_p(g, p);

  vector<map<int64_t, int64_t>> colors_in_subtree(n);
  vector<int64_t> sum(n);
  solve_case2(g, colors, p, sum, colors_in_subtree, 0, unique_color);

  for (size_t v = 0; v < n; ++v) {
    std::cout << sum[v] << " ";
  }
  std::cout << "\n";

  return 0;
}
