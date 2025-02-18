#include <cstdlib>
#include <iostream>

using std::abs;

static int gcd(int a, int b) { return b == 0 ? abs(a) : gcd(b, a % b); }

static int lcm(int a, int b) {
  auto g = gcd(a, b);
  return g != 0 ? abs(a * b) / g : 0;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << lcm(a, b) << "\n";

  return 0;
}
