#include "cpp/warmup/erato.hpp"

#include <cstddef>
#include <vector>

namespace warmup {

/*
 * Fill the table by following values: a_i = true when and only when i
 * is a prime number.
 */
void erato_fill(std::vector<bool> &table) {
  const std::size_t size = table.size();

  if (size <= 2) {
    std::fill(table.begin(), table.end(), false);
    return;
  }

  std::fill(table.begin(), table.end(), true);

  table[0] = table[1] = false;

  // See comment for internal cycle.
  for (std::size_t p = 2; p * p < size; ++p) {
    if (!table[p]) {
      continue;
    }

    // No need to mark numbers < p^2, because every multiplier less
    // than p already marked due to previous steps.
    for (std::size_t j = p * p; j < size; j += p) {
      table[j] = false;
    }
  }
}

}  // namespace warmup
