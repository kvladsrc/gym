#ifndef CPP_WARMUP_SETS_IN_TREE_H_
#define CPP_WARMUP_SETS_IN_TREE_H_

#include <cstdint>
#include <unordered_map>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<int>>;

struct node {
  std::unordered_map<int64_t, int64_t> table;
  int64_t sum{};
  int64_t max_color{};
};

node *merge(node *a, node *b);
node *solve(const graph &g, std::vector<int64_t> &res,
            const std::vector<int64_t> &colors, int start, int p);

}  // namespace warmup

#endif  // CPP_WARMUP_SETS_IN_TREE_H_
