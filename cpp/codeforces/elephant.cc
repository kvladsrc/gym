#include <iostream>

int main(int argc, char* argv[]) {
  int dist;
  std::cin >> dist;
  if (dist % 5)
    std::cout << dist / 5 + 1 << "\n";
  else
    std::cout << dist / 5 << "\n";

  return 0;
}
