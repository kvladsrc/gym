#ifndef CPP_WARMUP_FORD_FULKERSON_H_
#define CPP_WARMUP_FORD_FULKERSON_H_

#include <cstddef>
#include <vector>

namespace warmup {

using adj_matrix = std::vector<std::vector<int>>;
using adj_list = std::vector<std::vector<std::size_t>>;

// Every capacity edge u -> v must appear in g in both directions.
int ford_fulkerson(adj_matrix m, const adj_list& g, std::size_t start,
                   std::size_t end);

}  // namespace warmup

#endif  // CPP_WARMUP_FORD_FULKERSON_H_
