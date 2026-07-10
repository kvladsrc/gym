#include "cpp/warmup/ford_fulkerson.hpp"

#include <algorithm>
#include <climits>
#include <queue>
#include <vector>

namespace warmup {

static int augment(adj_matrix& m, const adj_list& g, std::size_t start,
                   std::size_t end) {
  std::queue<std::size_t> q;
  q.push(start);

  std::vector<bool> visited(g.size(), false);
  std::vector<std::size_t> p(g.size());
  std::vector<int> f(g.size());

  visited[start] = true;
  p[start] = start;
  f[start] = INT_MAX;

  bool found = false;

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();

    if (cur == end) {
      found = true;
      break;
    }

    for (auto n : g[cur]) {
      if (m[cur][n] <= 0 || visited[n]) {
        continue;
      }
      visited[n] = true;
      p[n] = cur;
      f[n] = std::min(f[cur], m[cur][n]);
      q.push(n);
    }
  }

  int res = 0;

  if (found) {
    res = f[end];
    auto cur = end;
    while (cur != start) {
      m[p[cur]][cur] -= res;
      m[cur][p[cur]] += res;
      cur = p[cur];
    }
  }

  return res;
}

int ford_fulkerson(adj_matrix m, const adj_list& g, std::size_t start,
                   std::size_t end) {
  int res = 0;

  if (start == end) {
    return 0;
  }

  while (true) {
    auto flow = augment(m, g, start, end);
    res += flow;

    if (flow == 0) {
      break;
    }
  }

  return res;
}

}  // namespace warmup
