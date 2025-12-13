#include "cpp/warmup/lca.hpp"

#include <algorithm>
#include <climits>
#include <cstddef>
#include <utility>
#include <vector>

namespace warmup {

lca::lca(const graph& g) {
  if (g.empty()) {
    return;
  }

  pre.resize(g.size());
  depths.resize(g.size());
  depths.front() = 0;
  ett(g, 0, 0);

  logs.resize(path.size() + 1);
  build_st();
}

int lca::depth(std::size_t v) { return depths[v]; }

void lca::ett(const graph& g, std::size_t v, std::size_t p) {
  pre[v] = path.size();
  depths[v] = depths[p] + 1;
  path.push_back(v);
  for (auto n : g[v]) {
    if (n == p) {
      continue;
    }
    ett(g, n, v);
    path.push_back(v);
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
      if (depth(st[cur_power - 1][i]) <
          depth(st[cur_power - 1][i + (window / 2)])) {
        st[cur_power][i] = st[cur_power - 1][i];
      } else {
        st[cur_power][i] = st[cur_power - 1][i + (window / 2)];
      }
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

std::size_t lca::query(std::size_t l, std::size_t r) {
  auto power = logs[r - l + 1];
  if (depth(st[power][l]) < depth(st[power][r - (1 << power) + 1])) {
    return st[power][l];
  }
  return st[power][r - (1 << power) + 1];
}

std::size_t lca::find(std::size_t a, std::size_t b) {
  if (a >= pre.size() || b >= pre.size()) {
    return 0;
  }

  auto prea = pre[a];
  auto preb = pre[b];
  if (preb < prea) {
    std::swap(prea, preb);
  }

  return query(prea, preb);
}

}  // namespace warmup
