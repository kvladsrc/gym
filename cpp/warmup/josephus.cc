#include <iostream>

static int josephus(int n, int k) {
  if (n == 1) {
    return 0;
  }

  return (josephus(n - 1, k) + k) % n;
}

int main() {
  int n = 0;
  int k = 0;
  std::cin >> n >> k;
  std::cout << josephus(n, k) << '\n';
  return 0;
}
