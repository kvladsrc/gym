#ifndef CPP_WARMUP_FORD_FULKERSON_H_
#define CPP_WARMUP_FORD_FULKERSON_H_

#include <cstddef>
#include <vector>

namespace warmup {

using adj_matrix = std::vector<std::vector<int>>;
using adj_list = std::vector<std::vector<std::size_t>>;

// Despite the name, this is the Edmonds-Karp algorithm: each augmenting path
// is found via BFS, which guarantees O(V * E^2) regardless of capacity values
// (plain Ford-Fulkerson with arbitrary path selection is not strongly
// polynomial and can even fail to terminate on irrational capacities).
//
// Correctness rests on the Ford-Fulkerson theorems:
//
// 1. Augmenting path theorem: a feasible flow is maximum if and only if there
//    is no source-to-sink path in the residual network. The driver loop halts
//    exactly when augment() can no longer find one and returns 0.
//
// 2. Max-flow min-cut theorem: the value of the maximum flow equals the
//    capacity of the minimum s-t cut, so the produced flow is optimal.
//
// 3. Integrality theorem: with integer capacities every augmentation carries
//    an integer amount of flow, so the algorithm terminates after finitely
//    many iterations and yields an integer maximum flow.
//
// Every capacity edge u -> v must appear in g in both directions.
int ford_fulkerson(adj_matrix m, const adj_list& g, std::size_t start,
                   std::size_t end);

}  // namespace warmup

#endif  // CPP_WARMUP_FORD_FULKERSON_H_
