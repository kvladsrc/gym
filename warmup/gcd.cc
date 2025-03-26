#include <cstdlib>
#include <iostream>

using std::abs;

namespace {

// EDGE_CASE: GCD(x, 0) = GCD(0, x) = x.
// EDGE_CASE: GCD(-n, a) = GCD(n, -a) = GCD(n, a).
int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << gcd(a, b) << "\n";

  return 0;
}
