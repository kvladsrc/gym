#ifndef CPP_WARMUP_EULERIAN_CYCLE_H_
#define CPP_WARMUP_EULERIAN_CYCLE_H_

#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

bool eulerian_cycle_exists(const graph& g);

}  // namespace warmup

#endif  // CPP_WARMUP_EULERIAN_CYCLE_H_
