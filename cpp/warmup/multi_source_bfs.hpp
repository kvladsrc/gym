#pragma once

#include <cstddef>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<std::size_t>>;

// Multi-source BFS. Finding the closest blue node.
void closest_blue(const graph& g, std::vector<int>& d,
                  const std::vector<char>& c);

}  // namespace warmup
