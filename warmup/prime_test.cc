#include <iostream>

namespace {

/*

  The algorithm runs in pseudopolynomial time (O(sqrt(n))), which is
  exponential when measured by the number of input bits.

*/

bool is_prime(int n) {
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

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  std::cout << (is_prime(n) ? "YES" : "NO") << "\n";

  return 0;
}
