#include <algorithm>
#include <iostream>

static int gcd_naive(int a, int b) {
  int current_gcd = 1;
  for (int d = 2; d <= a && d <= b; d++) {
    if (a % d == 0 && b % d == 0) {
      current_gcd = std::max(d, current_gcd);
    }
  }
  return current_gcd;
}

static int gcd_fast(int a, int b) {
  if ((a % b) == 0) {
    return b;
  }

  return gcd_fast(b, a % b);
}

int main() {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << gcd_naive(a, b) << "\n";
  std::cout << gcd_fast(a, b) << '\n';
  return 0;
}
