#include "cpp/warmup/nth_element.hpp"

#include <cstddef>
#include <optional>
#include <utility>
#include <vector>

namespace warmup {

int partition(std::vector<int>& a, int start, int end) {
  if (start >= end) {
    return start;
  }

  int const pivot = a[end];

  // Index less never used before been incremented.
  int less = start - 1;
  int more = start;

  while (more < end) {
    if (a[more] < pivot) {
      std::swap(a[++less], a[more]);
    }

    more++;
  }
  std::swap(a[++less], a[end]);

  return less;
}

int nth_element_recursive(std::vector<int>& a, int n, int start, int end) {
  auto pivot = partition(a, start, end);

  if (pivot == n) {
    return a[pivot];
  }

  if (pivot > n) {
    return nth_element_recursive(a, n, start, pivot - 1);
  }

  return nth_element_recursive(a, n, pivot + 1, end);
}

std::optional<int> nth_element_median(std::vector<int>& a) {
  if (a.empty()) {
    return std::nullopt;
  }
  int n = a.size();
  return nth_element_recursive(a, (n / 2), 0, n - 1);
}

}  // namespace warmup
