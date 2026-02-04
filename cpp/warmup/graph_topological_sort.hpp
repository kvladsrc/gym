#ifndef CPP_WARMUP_GRAPH_TOPOLOGICAL_SORT_H_
#define CPP_WARMUP_GRAPH_TOPOLOGICAL_SORT_H_

#include <cstddef>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

void topological_sort(graph& g, std::vector<int>& rank);

}  // namespace warmup

#endif  // CPP_WARMUP_GRAPH_TOPOLOGICAL_SORT_H_
