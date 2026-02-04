#ifndef CPP_WARMUP_BIPARTITENESS_CHECKER_H_
#define CPP_WARMUP_BIPARTITENESS_CHECKER_H_

#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

bool bipartiteness_test(const graph& g);

}  // namespace warmup

#endif  // CPP_WARMUP_BIPARTITENESS_CHECKER_H_
