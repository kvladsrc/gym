#include "cpp/warmup/sets_in_tree.hpp"

#include <utility>
#include <vector>

using std::vector;

namespace warmup {

node *merge(node *a, node *b) {
  if (a->table.size() < b->table.size()) {
    std::swap(a, b);
  }

  for (auto it : b->table) {
    a->table[it.first] += it.second;
    if (a->table[it.first] > a->max_color) {
      a->max_color = a->table[it.first];
      a->sum = it.first;
    } else if (a->table[it.first] == a->max_color) {
      a->sum += it.first;
    }
  }

  return a;
}

node *solve(const graph &g, vector<int64_t> &res, const vector<int64_t> &colors,
            int start, int p) {
  auto *n = new node();
  n->table[colors[start]] = 1;
  n->max_color = 1;
  n->sum = colors[start];

  for (auto neighbor : g[start]) {
    if (neighbor != p) {
      n = merge(n, solve(g, res, colors, neighbor, start));
    }
  }

  res[start] = n->sum;
  return n;
}

}  // namespace warmup
