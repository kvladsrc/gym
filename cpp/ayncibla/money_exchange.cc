#include <iostream>

static int get_change(int m) {
  // m in {0, 1, 2}
  if (m < 3) {
    return m;
  }

  // m in {3, 4}
  if (m < 5) {
    return 1;
  }

  int const q = m / 4;
  m = m % 4;

  switch (m) {
    case 0:
      return q;
    case 1:
      return q + 1;
    case 2:
      // Return 4, take 3, 3 coins;
      return q + 1;
    default:
      // m == 3
      return q + 1;
  }
}

int main() {
  int m = 0;
  std::cin >> m;
  std::cout << get_change(m) << '\n';
}
