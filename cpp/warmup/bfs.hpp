#ifndef CPP_WARMUP_BFS_H_
#define CPP_WARMUP_BFS_H_

#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

void cc_bfs(const graph &g, int v, std::vector<int> &cc_table, int cc);

}  // namespace warmup

#endif  // CPP_WARMUP_BFS_H_
