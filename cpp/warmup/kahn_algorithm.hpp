#ifndef CPP_WARMUP_KAHN_ALGORITHM_SORT_H_
#define CPP_WARMUP_KAHN_ALGORITHM_SORT_H_

#include <cstddef>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<std::size_t>>;

void topological_sort(graph& g, std::vector<std::size_t>& order);

}  // namespace warmup

#endif  // CPP_WARMUP_GRAPH_TOPOLOGICAL_SORT_H_
