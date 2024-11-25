#include <iostream>

int main(int argc, char *argv[]) {
  int n, prev, groups = 1;
  std::cin >> n;

  n--;
  std::cin >> prev;

  while (n) {
    int elem;
    std::cin >> elem;
    if (elem != prev) {
      groups++;
      prev = elem;
    }
    n--;
  }

  std::cout << groups << "\n";

  return 0;
}
