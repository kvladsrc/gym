#include "cpp/warmup/bfs.hpp"

#include <iostream>
#include <queue>
#include <string>
#include <vector>

using std::queue;
using std::string;
using std::vector;

namespace warmup {

void cc_bfs(const graph &g, int v, vector<int> &cc_table, int cc) {
  queue<int> q;
  q.push(v);
  while (!q.empty()) {
    auto nv = q.front();
    q.pop();

    if (cc_table[nv] != 0) {
      continue;
    }

    cc_table[nv] = cc;

    for (auto nnv : g[nv]) {
      q.push(nnv);
    }
  }
}

}  // namespace warmup
