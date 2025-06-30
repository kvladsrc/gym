#ifndef CPP_WARMUP_DIJKSTRA_H_
#define CPP_WARMUP_DIJKSTRA_H_

#include <climits>
#include <utility>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<std::pair<int, int>>>;

int dijkstra(graph &g, int start, int end);

}  // namespace warmup

#endif  // CPP_WARMUP_DIJKSTRA_H_
