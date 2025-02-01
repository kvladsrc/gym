#include <cstdlib>
#include <iostream>
#include <string>

using std::abs;
using std::string;

static int gcd(int a, int b) {
  if (a == 0) {
    return b;
  }

  if (b == 0) {
    return a;
  }

  if (a % b == 0) {
    return abs(b);
  }

  return gcd(b, a % b);
}

int main(int /*argc*/, char* /*argv*/[]) {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << gcd(a, b) << "\n";

  return 0;
}
