#include <iostream>

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  int available = 0;
  while (n) {
    int in, cap;
    std::cin >> in >> cap;
    if (cap >= in + 2) available++;
    n--;
  }
  std::cout << available << "\n";

  return 0;
}
