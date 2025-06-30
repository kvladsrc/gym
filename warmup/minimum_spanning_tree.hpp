#ifndef CPP_WARMUP_MINIMUM_SPANNING_TREE_H_
#define CPP_WARMUP_MINIMUM_SPANNING_TREE_H_

#include <utility>
#include <vector>

namespace warmup {

using edge = std::pair<int, std::pair<int, int>>;

std::vector<edge> kruskal(int v, std::vector<edge> &edges);

}  // namespace warmup

#endif  // CPP_WARMUP_MINIMUM_SPANNING_TREE_H_
