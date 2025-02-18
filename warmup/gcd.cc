#include <cstdlib>
#include <iostream>

using std::abs;

static int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

int main(int /*argc*/, char* /*argv*/[]) {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << gcd(a, b) << "\n";

  return 0;
}
