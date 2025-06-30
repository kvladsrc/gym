#ifndef CPP_WARMUP_NEGATIVE_CYCLE_DETECTION_H_
#define CPP_WARMUP_NEGATIVE_CYCLE_DETECTION_H_

#include <climits>
#include <vector>

namespace warmup {

using adj_matrix = std::vector<std::vector<int>>;

bool negative_cycle_detection(adj_matrix& dists);

}  // namespace warmup

#endif  // CPP_WARMUP_NEGATIVE_CYCLE_DETECTION_H_
