#include <iostream>

// Useful prime number.
const int modulo = 1000000007;

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

/*
  b * b^-1 = 1 mod m
  a / b mod m = a * b^-1 mod m.
  b^-1 = b^m-2 mod m by Fermat's little theorem since m is prime.

  combinations(k, n) = n! / k!(n - k)! = n! * (k!(n - k)!)^-1.
*/
int combinations(int k, int n) {
  if (k > n) {
    return 0;
  }

  auto r = factorial(n);
  auto d = factorial(k) * 1ll * factorial(n - k) % modulo;
  d = fast_power(d, modulo - 2);
  r = r * 1ll * d % modulo;

  return r;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  int k = 0;
  std::cin >> n >> k;
  std::cout << combinations(k, n) << "\n";

  return 0;
}
