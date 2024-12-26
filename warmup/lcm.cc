#include <cstdlib>
#include <iostream>
#include <string>

using std::abs;
using std::string;

static int gcd(int a, int b) {
  if (a % b == 0) {
    return abs(b);
  }
  return gcd(b, a % b);
}

static int lcm(int a, int b) {
  auto g = gcd(a, b);
  return a * b / g;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << lcm(a, b) << "\n";

  return 0;
}
