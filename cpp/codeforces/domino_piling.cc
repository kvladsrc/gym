#include <iostream>

// O(1)
int fill(int n, int m) {
  if (n % 2 == 0 || m % 2 == 0) {
    return (n * m) / 2;
  } else {
    return (n * m) / 2;
  }
}

int main() {
  int n, m;
  std::cin >> n >> m;
  std::cout << fill(n, m) << "\n";
}
