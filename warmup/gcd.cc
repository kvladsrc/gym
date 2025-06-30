#include "cpp/warmup/gcd.hpp"

#include <cstdlib>
#include <iostream>

using std::abs;

namespace warmup {

// EDGE_CASE: GCD(x, 0) = GCD(0, x) = x.
// EDGE_CASE: GCD(-n, a) = GCD(n, -a) = GCD(n, a).
int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

}  // namespace warmup
