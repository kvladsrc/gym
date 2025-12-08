#ifndef CPP_WARMUP_FORD_BELLMAN_H_
#define CPP_WARMUP_FORD_BELLMAN_H_

#include <vector>

namespace warmup {

std::vector<int> ford_bellman(int n,
                              const std::vector<std::vector<int>>& graph);

}  // namespace warmup

#endif  // CPP_WARMUP_FORD_BELLMAN_H_
