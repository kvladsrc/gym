#include "cpp/warmup/binary_search_with_duplicates.hpp"

#include <cstddef>
#include <iostream>
#include <optional>
#include <vector>

namespace warmup {

std::optional<std::size_t> lower_bound(const std::vector<int> &a, int value) {
  // EDGE_CASE: empty vector do not contain any elements.
  if (a.empty()) {
    return std::nullopt;
  }

  // Negative values needed to break cycle on r = -1, l = 0.
  ssize_t l = 0;
  ssize_t r = a.size() - 1;
  std::optional<std::size_t> res = std::nullopt;

  while (l <= r) {
    // (l + r) / 2 can lead to overvlow.
    auto const mid = l + ((r - l) / 2);

    if (a[mid] >= value) {
      r = mid - 1;
      res = mid;
    } else {
      l = mid + 1;
    }
  }

  return res;
}

}  // namespace warmup
