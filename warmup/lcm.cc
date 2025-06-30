#include "cpp/warmup/lcm.hpp"

#include <cstdlib>
#include <iostream>

using std::abs;

namespace warmup {

int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

int lcm(int a, int b) {
  auto g = gcd(a, b);

  // EDGE_CASE: GCD(a, b) = 0.
  return g != 0 ? abs(a * b) / g : 0;
}

}  // namespace warmup
