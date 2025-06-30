#include "cpp/warmup/fast_power.hpp"

#include <cstdint>
#include <iostream>
#include <string>

using std::string;

namespace warmup {

/*
 * Compute x^y mod m by O(log y) time complexity.
 */

int64_t fast_power(int64_t x, int64_t y, int64_t m) {
  if (y == 1) {
    return x;
  }

  if (y == 0) {
    return 1;
  }

  if ((y % 2) != 0) {
    auto buf = fast_power(x, y - 1, m);
    buf = (buf * x) % m;
    return buf;
  }

  return fast_power((x * x) % m, y / 2, m);  // x^y = (x^2)^y/2
}

}  // namespace warmup
