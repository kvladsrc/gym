#include <iostream>

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  while (n) {
    int buf;
    std::cin >> buf;
    if (buf) break;
    n--;
  }

  if (n) {
    std::cout << "HARD"
              << "\n";
  } else {
    std::cout << "EASY"
              << "\n";
  }

  return 0;
}
