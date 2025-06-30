#include "cpp/warmup/3sum.hpp"

#include <cstddef>
#include <vector>

using std::vector;

namespace warmup {

/*
 * This is a canonical 3SUM problem—a model problem to which many
 * others, especially geometric ones, are often reduced.  The
 * algorithm implemented here runs in O(n^2) time, which is considered
 * optimal.
 */

bool three_sum(const vector<int> &a, int sum) {
  // EDGE_CASE: input vector has less than 3 elements.
  if (a.size() < 3) {
    return false;
  }

  // a has to be sorted.
  for (std::size_t f = 0; f < a.size() - 2; ++f) {
    std::size_t l = f + 1;
    std::size_t r = a.size() - 1;

    while (l < r) {
      auto local = a[f] + a[l] + a[r];

      if (local < sum) {
        l++;
      } else if (local > sum) {
        r--;
      } else {
        return true;
      }
    }
  }

  return false;
}

}  // namespace warmup
