#ifndef CPP_WARMUP_CENTROID_H_
#define CPP_WARMUP_CENTROID_H_

#include <cstddef>
#include <vector>

namespace warmup {

using graph = std::vector<std::vector<size_t>>;

int fill_sizes(const graph& g, std::vector<int>& s, size_t v, size_t p);
size_t centroid(const graph& g, const std::vector<int>& sizes, size_t v,
                size_t p);

}  // namespace warmup

#endif  // CPP_WARMUP_CENTROID_H_
