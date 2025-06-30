#include "cpp/warmup/dfs.hpp"

#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

namespace warmup {

void cc_dfs(graph &g, int v, vector<int> &cc_table, int cc) {
  if (cc_table[v] != 0) {
    return;
  }

  cc_table[v] = cc;

  for (auto nv : g[v]) {
    cc_dfs(g, nv, cc_table, cc);
  }
}

}  // namespace warmup
