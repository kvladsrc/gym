#include <iostream>

int main(int argc, char* argv[]) {
  int a, b;
  std::cin >> a >> b;
  int year = 0;
  while (a <= b) {
    a *= 3;
    b *= 2;
    year++;
  }
  std::cout << year << "\n";
  return 0;
}
