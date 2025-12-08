#include "cpp/warmup/multi_source_bfs.hpp"

#include <algorithm>
#include <climits>
#include <queue>
#include <vector>

namespace warmup {

void closest_blue(const graph& g, std::vector<int>& d,
                  const std::vector<char>& c) {
  std::fill(d.begin(), d.end(), INT_MAX);

  std::queue<std::size_t> q;

  for (std::size_t idx = 0; idx < c.size(); ++idx) {
    if (c[idx] == 'B') {
      q.push(idx);
      d[idx] = 0;
    }
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

}  // namespace warmup
