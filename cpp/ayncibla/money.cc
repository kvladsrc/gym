#include <iostream>

static int get_change(int m) {
  int n = 0;
  while (m > 0) {
    n++;

    if (m >= 10) {
      m -= 10;
      continue;
    }

    if (m >= 5) {
      m -= 5;
      continue;
    }

    m--;
  }
  return n;
}

int main() {
  int m = 0;
  std::cin >> m;
  std::cout << get_change(m) << '\n';
}
