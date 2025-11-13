#ifndef CPP_WARMUP_FLOYD_WARSHALL_H_
#define CPP_WARMUP_FLOYD_WARSHALL_H_

#include <climits>
#include <vector>

namespace warmup {

using adj_matrix = std::vector<std::vector<int>>;

void floyd_warshall(adj_matrix& dists);

}  // namespace warmup

#endif  // CPP_WARMUP_FLOYD_WARSHALL_H_
