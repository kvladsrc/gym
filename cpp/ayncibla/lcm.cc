#include <cstdint>
#include <iostream>

static int gcd_fast(int a, int b) {
  if (b == 1) {
    return 1;
  }

  if ((a % b) == 0) {
    return b;
  }

  return gcd_fast(b, a % b);
}

static int64_t lcm_naive(int a, int b) {
  for (int64_t l = 1; l <= static_cast<int64_t>(a) * b; ++l) {
    if (l % a == 0 && l % b == 0) {
      return l;
    }
  }

  return static_cast<int64_t>(a) * b;
}

static int64_t lcm_fast(int a, int b) {
  auto gcd = gcd_fast(a, b);
  return static_cast<int64_t>(a / gcd) * b;
}

int main() {
  int a = 0;
  int b = 0;
  std::cin >> a >> b;
  std::cout << lcm_naive(a, b) << "\n";
  std::cout << lcm_fast(a, b) << '\n';
  return 0;
}
