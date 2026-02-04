#include <iostream>

int main(int argc, char* argv[]) {
  int n, k;
  std::cin >> n >> k;
  while (k) {
    k--;
    if (n % 10) {
      n -= 1;
    } else {
      n /= 10;
    }
  }

  std::cout << n << "\n";

  return 0;
}
