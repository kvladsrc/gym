#include "cpp/warmup/kahn_algorithm.hpp"

#include <cstddef>
#include <queue>
#include <vector>

namespace warmup {

void topological_sort(graph& g, std::vector<std::size_t>& order) {
  // Fill the table by number of input edges for every vertex.
  std::vector<int> num_in(g.size(), 0);

  for (const auto& v : g) {
    for (auto to : v) {
      num_in[to]++;
    }
  }

  // Add all source nodes (without input edges) in queue.
  std::queue<std::size_t> source_nodes;

  for (std::size_t i = 0; i < num_in.size(); ++i) {
    if (num_in[i] == 0) {
      source_nodes.push(i);
    }
  }

  // Keep adding source nodes from queue to result vector and removing
  // the nodes from original graph with all edges. If some nodes
  // became source (0 input edges) add them to queue.
  order.resize(g.size());
  std::size_t idx = 0;
  while (!source_nodes.empty()) {
    auto cur = source_nodes.front();
    source_nodes.pop();
    order[idx++] = cur;

    for (auto n : g[cur]) {
      num_in[n]--;
      if (num_in[n] == 0) {
        source_nodes.push(n);
      }
    }
  }
}

}  // namespace warmup
