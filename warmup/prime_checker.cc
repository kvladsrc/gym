#include "cpp/warmup/prime_checker.hpp"

#include <iostream>

namespace warmup {

/*
 * The algorithm runs in pseudopolynomial time (O(sqrt(n))), which is
 * exponential when measured by the number of input bits.
 */

bool is_prime(int n) {
  // EDGE_CASE: Numbers smaller or equal to 1 is not primes by
  // definition.
  if (n <= 1) {
    return false;
  }

  for (int d = 2; d * d <= n; ++d) {
    if (n % d == 0) {
      return false;
    }
  }

  return true;
}

}  // namespace warmup
