#ifndef CPP_WARMUP_STRONGLY_CONNECTED_COMPONENTS_H_
#define CPP_WARMUP_STRONGLY_CONNECTED_COMPONENTS_H_

#include <utility>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

void dfs(const graph &g, int start, int &clock, std::vector<int> &rank);
void dfs_cc(const graph &g, int start, int cc, std::vector<int> &cc_table);
void topological_sort(const graph &g, std::vector<int> &rank);
std::vector<int> find_scc(int v, const std::vector<std::pair<int, int>> &edges);

}  // namespace warmup

#endif  // CPP_WARMUP_STRONGLY_CONNECTED_COMPONENTS_H_
