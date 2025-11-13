#include "cpp/warmup/combinations.hpp"

#include <iostream>

namespace warmup {

/*
 * Combinations formula:
 *
 * C(n, k) = n! / k!(n - k)!
 *
 * C(n, k) grows very fast, lets calculate C(n, k) mod m instead,
 * where m = 1000000007 (useful prime number) by replacing a / b with
 * a * 1/b.
 *
 * b^-1 mod m = b^m-2 mod m by Fermat's little theorem since m is
 * prime. So solution is:
 *
 * C(n, k) = n! / k!(n - k)! = n! * (k!(n - k)!)^-1.
 */

int fast_power(int a, int p) {
  if (p == 0) {
    return 1;
  }

  if (p % 2 == 0) {
    // Preventing overflow by conversion to long long.
    a = a * 1ll * a % modulo;
    return fast_power(a, p / 2);
  }

  auto r = fast_power(a, p - 1);
  r = r * 1ll * a % modulo;
  return r;
}

int factorial(int a) {
  int res = 1;
  for (int i = 1; i <= a; ++i) {
    res = res * 1ll * i % modulo;
  }
  return res;
}

int combinations(int n, int k) {
  // EDGE_CASE: Cannot select k elements from n if k > n.
  if (k > n) {
    return 0;
  }

  auto r = factorial(n);
  auto d = factorial(k) * 1ll * factorial(n - k) % modulo;
  d = fast_power(d, modulo - 2);
  r = r * 1ll * d % modulo;

  return r;
}

}  // namespace warmup
