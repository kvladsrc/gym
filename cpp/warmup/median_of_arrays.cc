#include "cpp/warmup/median_of_arrays.hpp"

#include <algorithm>
#include <climits>
#include <vector>

namespace warmup {

int get_elem_by_num(const std::vector<int>& a, size_t num) {
  if (num == 0) {
    return INT_MIN;
  }

  if (num > a.size()) {
    return INT_MAX;
  }

  return a[num - 1];
}

// n -- is the index of the element in merged array.
int nth_of_arrays(const std::vector<int>& a, const std::vector<int>& b,
                  size_t n) {
  if (n >= (a.size() + b.size())) {
    return -1;
  }

  // Minimum number of elements needed to pick from a.
  size_t l = (n < b.size() ? 0 : n - b.size() + 1);

  // Maximal number of elements needed to pick from a.
  size_t r = (n < a.size() ? n + 1 : a.size());

  int res = -1;

  while (l <= r) {
    auto m = (l + r) / 2;
    auto need_b = n - m + 1;

    if (std::min(get_elem_by_num(a, m + 1), get_elem_by_num(b, need_b + 1)) >=
        std::max(get_elem_by_num(a, m), get_elem_by_num(b, need_b))) {
      res = std::max(get_elem_by_num(a, m), get_elem_by_num(b, need_b));
      break;
    }

    if (get_elem_by_num(a, m + 1) < get_elem_by_num(b, need_b)) {
      l = m + 1;
    } else {
      r = m - 1;
    }
  }

  return res;
}

}  // namespace warmup
