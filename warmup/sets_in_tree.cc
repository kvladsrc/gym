#include <cmath>
#include <iostream>
#include <unordered_map>
#include <vector>

using std::unordered_map;
using std::vector;

using graph = vector<vector<int>>;

struct node {
  unordered_map<int64_t, int64_t> table;
  int64_t sum{};
  int64_t max_color{};
  node() = default;
};

static node *merge(node *a, node *b) {
  auto *mx = a;
  auto *mn = b;
  if (a->table.size() < b->table.size()) {
    std::swap(mx, mn);
  }

  for (auto p : mn->table) {
    mx->table[p.first] += p.second;
    if (mx->table[p.first] > mx->max_color) {
      mx->max_color = mx->table[p.first];
      mx->sum = p.first;
      continue;
    }

    if (mx->table[p.first] == mx->max_color) {
      mx->sum += p.first;
    }
  }

  delete mn;
  return mx;
}

node *solve(graph &g, vector<int64_t> &res, vector<int64_t> &colors, int start,
            int p) {
  auto *m = new node;
  m->max_color = 1;
  m->sum = colors[start];
  m->table[colors[start]]++;

  for (auto n : g[start]) {
    if (n == p) {
      continue;
    }

    auto *nm = solve(g, res, colors, n, start);

    m = merge(m, nm);
  }

  res[start] = m->sum;
  return m;
}

int main(int /*argc*/, char * /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;

  vector<int64_t> colors(n);
  for (auto &i : colors) {
    std::cin >> i;
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

  vector<int64_t> res(n);
  solve(g, res, colors, 0, 0);

  for (auto i : res) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
